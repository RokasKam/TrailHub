using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TrailHub.BuildingBlocks.Web;

/// <summary>
/// Base for every TrailHub controller. Controllers stay thin: bind the request, call one service
/// method, and return its value. Services report failures by throwing, which
/// <see cref="TrailHubExceptionHandler"/> turns into problem responses.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public abstract class ApiController : ControllerBase;
