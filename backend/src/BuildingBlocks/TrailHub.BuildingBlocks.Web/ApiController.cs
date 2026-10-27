using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.BuildingBlocks.Web;

/// <summary>
/// Base for every TrailHub controller. Controllers stay thin: bind the request, call one
/// application handler, and translate its <see cref="Result"/> into an HTTP response.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public abstract class ApiController : ControllerBase
{
    /// <summary>200 with the value, or the error as problem details.</summary>
    protected ActionResult<T> FromResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemFor(result.Error!);

    /// <summary>A custom success response (e.g. 201 Created), or the error as problem details.</summary>
    protected ActionResult<T> FromResult<T>(Result<T> result, Func<T, ActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : ProblemFor(result.Error!);

    /// <summary>204, or the error as problem details.</summary>
    protected IActionResult FromResult(Result result) =>
        result.IsSuccess ? NoContent() : ProblemFor(result.Error!);

    protected ObjectResult ProblemFor(Error error)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError,
            },
            title: error.Code,
            detail: error.Message);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }
}
