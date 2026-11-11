using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Application.Exceptions;

namespace TrailHub.BuildingBlocks.Web;

/// <summary>
/// Turns exceptions into RFC 9457 problem responses. Service exceptions (<see cref="AppException"/>) keep
/// their status and code; anything else is a bad request, a concurrency conflict, or a logged 500.
/// </summary>
internal sealed class TrailHubExceptionHandler(ILogger<TrailHubExceptionHandler> logger, IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail, code) = Describe(exception);
        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = status;
        var problem = new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        };
        if (code is not null)
        {
            problem.ProblemDetails.Extensions["code"] = code;
        }

        return await problemDetails.TryWriteAsync(problem);
    }

    private static (int Status, string Title, string? Detail, string? Code) Describe(Exception exception) => exception switch
    {
        AppException app => (StatusFor(app), app.Code, app.Message, app.Code),

        // Value objects throw ArgumentException when handed impossible values; that's a bad request, not a crash.
        ArgumentException or BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message, null),

        // Two people changed the same row at once (optimistic concurrency); the client should reload and retry.
        // Matched by name so this layer doesn't depend on EF Core.
        _ when exception.GetType().Name == "DbUpdateConcurrencyException" =>
            (StatusCodes.Status409Conflict, "Someone else changed this at the same time", "Reload and try again.", null),

        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", null, null),
    };

    private static int StatusFor(AppException exception) => exception switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        ForbiddenException => StatusCodes.Status403Forbidden,
        ConflictException => StatusCodes.Status409Conflict,
        ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest,
    };
}
