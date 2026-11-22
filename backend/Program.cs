using JwtDemo.BackgroundServices;
using JwtDemo.Core;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

var isDev = builder.Environment.IsDevelopment();

builder.Services.ConfigureHttpJsonOptions(o => JsonConfig.ConfigureJsonSerialization(o.SerializerOptions, isDev));
builder.Services.AddSignalR()
       .AddJsonProtocol(o =>
       {
           o.PayloadSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
       });
builder.Services.ConfigureProblemDetails();
builder.Services.ConfigureOpenApi();
builder.Services.ConfigureForwardedHeaders();
builder.Services.LoadConfiguration(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddRateLimiting();
builder.Services.ConfigureCors(builder.Configuration);
builder.Services.ConfigureDatabase(builder.Configuration);
builder.Services.AddHostedService<TimeUpdateService>();
Setup.ConfigureValidation();

var app = builder.Build();

await app.UpdateDatabase();

// Not using HTTPS here: in production the backend _has_ to run behind a reverse proxy, which handles TLS (and HSTS).
// Tokens are as good as passwords - they must never be sent unencrypted over a network!
app.UseForwardedHeaders();
app.UseExceptionHandler();

if (isDev)
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "JWT Demo"));
}

app.UseCors(Setup.CorsPolicyName);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapApplicationEndpoints(isDev);

await app.RunAsync();
