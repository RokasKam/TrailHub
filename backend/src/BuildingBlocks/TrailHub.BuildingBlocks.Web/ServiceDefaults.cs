using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web.Authentication;

namespace TrailHub.BuildingBlocks.Web;

public static class ServiceDefaults
{
    /// <summary>Tag health checks with this to include them in <c>/health/ready</c>.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Cross-cutting setup every TrailHub API shares: auth, controllers, problem details, JSON, Swagger, health.</summary>
    /// <param name="apiTitle">Shown as the Swagger document title, e.g. "TrailHub Trails API".</param>
    public static WebApplicationBuilder AddTrailHubServiceDefaults(this WebApplicationBuilder builder, string apiTitle)
    {
        builder.AddTrailHubAuthentication();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
        builder.Services
            .AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddTrailHubSwagger(apiTitle);
        builder.Services.AddHealthChecks();

        return builder;
    }

    public static WebApplication UseTrailHubServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseTrailHubSwagger(); // before auth: the docs themselves are public, the endpoints aren't
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag) }).AllowAnonymous();
        return app;
    }

    private sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger, IProblemDetailsService problemDetails)
        : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
        {
            // Value objects throw ArgumentException when handed impossible values; that's a bad request, not a crash.
            var isBadInput = exception is ArgumentException or BadHttpRequestException;

            // Two people changed the same row at once (optimistic concurrency); the client should reload and retry.
            // Matched by name so this layer doesn't depend on EF Core.
            var isConflict = exception.GetType().Name == "DbUpdateConcurrencyException";
            if (!isBadInput && !isConflict)
            {
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            context.Response.StatusCode = isBadInput ? StatusCodes.Status400BadRequest
                : isConflict ? StatusCodes.Status409Conflict
                : StatusCodes.Status500InternalServerError;
            return await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails =
                {
                    Status = context.Response.StatusCode,
                    Title = isBadInput ? "Invalid request" : isConflict ? "Someone else changed this at the same time" : "An unexpected error occurred",
                    Detail = isBadInput ? exception.Message : isConflict ? "Reload and try again." : null,
                },
            });
        }
    }
}
