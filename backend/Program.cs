using System.Text.Json.Serialization;
using JwtDemo.BackgroundServices;
using JwtDemo.Core;
using JwtDemo.Hubs;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<JsonOptions>(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
});
builder.Services.AddSignalR()
       .AddJsonProtocol(o =>
       {
           o.PayloadSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
       });
// unhandled exceptions are logged and turned into a generic 500 ProblemDetails response (no stack traces for clients!)
builder.Services.AddProblemDetails();
builder.Services.ConfigureOpenApi();
builder.Services.LoadConfiguration(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddRateLimiting();
builder.Services.ConfigureCors(builder.Configuration);
builder.Services.ConfigureDatabase(builder.Configuration);
builder.Services.AddHostedService<TimeUpdateService>();

var app = builder.Build();

await app.UpdateDatabase();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "JWT Demo"));
}
else
{
    // Tokens are as good as passwords - outside of local development they must only ever be sent via HTTPS
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(Setup.CorsPolicyName);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TimeHub>($"{Setup.HubBasePath}/time", o =>
   {
       // SignalR checks the token only when the connection is established. Without this option, a connection
       // would stay authenticated forever, even after the access token expired.
       o.CloseOnAuthenticationExpiration = true;
   })
   // anonymous users may connect as well (they just get less frequent updates)
   .AllowAnonymous();

app.Run();
