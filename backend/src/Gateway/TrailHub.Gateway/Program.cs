using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// The gateway only routes and protects the edge. Every service validates the Auth0 token itself,
// so a request that bypasses the gateway (e.g. from inside the cluster) is still authenticated.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-client", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Partition by bearer token when present (per user/device), otherwise by IP.
            context.Request.Headers.Authorization.ToString() is { Length: > 0 } auth ? auth : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddHealthChecks();

var app = builder.Build();

// One Swagger UI for every service. Each service's document is proxied at /openapi/{service}.json,
// and "Try it out" calls go back through the gateway, so only port 8080 needs to be reachable.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "TrailHub API";
        foreach (var (title, service) in app.Configuration.GetSection("Swagger:Services").Get<Dictionary<string, string>>() ?? [])
        {
            options.SwaggerEndpoint($"/openapi/{service}.json", title);
        }
    });
}

app.UseCors();
app.UseRateLimiter();
app.UseWebSockets();
app.MapHealthChecks("/health/live");
app.MapReverseProxy();

await app.RunAsync();
