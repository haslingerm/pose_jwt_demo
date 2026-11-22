using System.Threading.RateLimiting;
using JwtDemo.Core.Auth;
using JwtDemo.Core.Products;
using JwtDemo.Core.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NodaTime;

namespace JwtDemo.Core;

public static class Setup
{
    public const string CorsPolicyName = "CorsPolicy";
    public const string AuthRateLimitPolicyName = "AuthRateLimitPolicy";
    public const string HubBasePath = "/hubs";

    // C# 14 extension block: all members in here are extension members for IServiceCollection
    extension(IServiceCollection services)
    {
        public void LoadConfiguration(IConfiguration configuration)
        {
            services.Configure<Settings>(configuration.GetSection(Settings.SectionKey));
            services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.SectionKey));
        }

        public void RegisterServices()
        {
            services.AddTransient<IAuthService, AuthService>();
            services.AddSingleton<ITokenProvider, TokenProvider>();

            // one UnitOfWork instance per request, available via both interfaces
            services.AddScoped<UnitOfWork>();
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
            services.AddScoped<ITransactionProvider>(sp => sp.GetRequiredService<UnitOfWork>());

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IProductService, ProductService>();

            services.AddSingleton<IClock>(SystemClock.Instance);
        }

        public void AddAuth(IConfiguration configuration)
        {
            var settings = configuration.GetSection(AuthSettings.SectionKey).Get<AuthSettings>()
                           ?? throw new InvalidOperationException("JWT settings are missing");
            settings.Validate();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(o =>
                    {
                        // keep the claim names as they are in the token ('name', 'role', 'sub'), instead of
                        // mapping them to the long legacy 'http://schemas.xmlsoap.org/...' claim types
                        o.MapInboundClaims = false;
                        o.TokenValidationParameters = new TokenValidationParameters
                        {
                            // the signature proves that we created the token and nobody tampered with it
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = settings.CreateSigningKey(),
                            // Only accept the algorithm we use ourselves. Otherwise, an attacker could try to make us
                            // validate a token with a different (e.g. weaker or 'none') algorithm.
                            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                            ValidateIssuer = true,
                            ValidIssuer = settings.TokenIssuer,
                            ValidateAudience = true,
                            ValidAudience = settings.TokenAudience,

                            ValidateLifetime = true,
                            RequireExpirationTime = true,
                            // Tolerance for clocks of different servers not being perfectly in sync. The default is
                            // 5 minutes, which would be long compared to our access token lifetime. We create and
                            // validate the tokens on the same server, so a small value is sufficient.
                            ClockSkew = TimeSpan.FromSeconds(5),

                            NameClaimType = AuthClaimTypes.Name,
                            RoleClaimType = AuthClaimTypes.Role
                        };

                        o.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                // This is required for SignalR: browsers cannot send an Authorization header
                                // when opening a WebSocket, so the client sends the token in the query string.
                                // We take it from there and process it as usual - but only for hub requests,
                                // since URLs (and thus the token) might end up in logs.
                                var accessToken = context.Request.Query["access_token"];
                                var path = context.HttpContext.Request.Path;

                                if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments(HubBasePath))
                                {
                                    context.Token = accessToken;
                                }

                                return Task.CompletedTask;
                            }
                        };
                    });

            var authorization = services.AddAuthorizationBuilder()
                                        // Secure by default: every endpoint requires an authenticated user, unless it
                                        // explicitly opts out with [AllowAnonymous]. So forgetting an [Authorize]
                                        // attribute does not accidentally open up an endpoint.
                                        .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                                                           .RequireAuthenticatedUser()
                                                           .Build());

            // one policy per role, named like the role - e.g. [Authorize(nameof(UserRole.Admin))]
            foreach (var role in Enum.GetNames<UserRole>())
            {
                authorization.AddPolicy(role, p => p.RequireRole(role));
            }
        }

        public void ConfigureOpenApi()
        {
            services.AddOpenApi(o => o.AddDocumentTransformer((document, _, _) =>
            {
                // allows us to enter the access token in the Swagger UI ('Authorize' button)
                const string SchemeName = "Bearer";
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT access token, as returned by the login endpoint"
                };
                document.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
                    }
                ];

                return Task.CompletedTask;
            }));
        }

        public void ConfigureCors(IConfiguration configuration)
        {
            var settings = configuration.GetSection(Settings.SectionKey).Get<Settings>();
            var corsOrigin = settings?.ClientOrigin;

            if (string.IsNullOrWhiteSpace(corsOrigin))
            {
                throw new InvalidOperationException("Client origin is missing");
            }

            services.AddCors(o =>
            {
                o.AddPolicy(CorsPolicyName, b =>
                {
                    // Only our own frontend may call the API from a browser - never use AllowAnyOrigin with auth!
                    // We don't need AllowCredentials: the token is sent in the Authorization header, not in a cookie.
                    b.WithOrigins(corsOrigin)
                     .AllowAnyMethod()
                     .AllowAnyHeader();
                });
            });
        }

        public void ConfigureDatabase(IConfiguration configuration)
        {
            var connString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connString))
            {
                throw new InvalidOperationException("Database path is missing");
            }

            services.AddDbContext<DatabaseContext>(optionsBuilder =>
            {
                optionsBuilder.UseSqlite(connString, dbOptions =>
                {
                    dbOptions.UseNodaTime();
                });
            });
        }

        public void AddRateLimiting()
        {
            services.AddRateLimiter(o =>
            {
                // 429 is the correct status code (the default would be 503 Service Unavailable)
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Each client (IP address) gets its own limit: this slows down password guessing, while an attacker
                // cannot lock out all other users. Behind a reverse proxy, you'd have to configure the
                // ForwardedHeaders middleware, otherwise all requests would seem to come from the proxy's IP.
                o.AddPolicy(AuthRateLimitPolicyName, context =>
                                RateLimitPartition.GetFixedWindowLimiter(
                                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                                    _ => new FixedWindowRateLimiterOptions
                                    {
                                        PermitLimit = 20,
                                        Window = TimeSpan.FromMinutes(1),
                                        QueueLimit = 0
                                    }));
            });
        }
    }

    extension(WebApplication app)
    {
        public async ValueTask UpdateDatabase()
        {
            // in a real application you would apply migrations manually after reviewing them

            await using var serviceScope = app.Services.CreateAsyncScope();
            var context = serviceScope.ServiceProvider.GetRequiredService<DatabaseContext>();

            await context.Database.MigrateAsync();
            await InsertDefaultProducts(context);

            return;

            static async ValueTask InsertDefaultProducts(DatabaseContext context)
            {
                if (await context.Products.AnyAsync())
                {
                    return;
                }

                Product[] products =
                [
                    new() { Name = "Rice", Price = 0.99M },
                    new() { Name = "Beans", Price = 1.29M },
                    new() { Name = "Spaghetti", Price = 1.50M },
                    new() { Name = "Bread", Price = 2.20M },
                    new() { Name = "Bottled Water", Price = 0.79M }
                ];

                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }
        }
    }
}
