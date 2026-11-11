using Microsoft.AspNetCore.Mvc;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.BuildingBlocks.Web;

namespace TrailHub.Activity.Api.Controllers;

/// <summary>Moderators: reported and hidden hike comments and photos.</summary>
[Route("api/activities/moderation")]
public sealed class ContentModerationController(IContentModerationService moderationService) : ApiController
{
    /// <summary>Comments and hike photos with open reports, and hidden ones, most reported first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ContentModerationItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ContentModerationItemResponse>>> Queue(CancellationToken ct) =>
        Ok(await moderationService.GetQueueAsync(ct));

    /// <summary>Restores, hides or deletes a comment. Resolves its open reports.</summary>
    [HttpPut("comments/{contentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModerateComment(Guid contentId, ModerateContentRequest body, CancellationToken ct)
    {
        await moderationService.ModerateCommentAsync(contentId, body, ct);
        return NoContent();
    }

    /// <summary>Restores, hides or deletes a hike photo (and its stored file). Resolves its open reports.</summary>
    [HttpPut("photos/{contentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModeratePhoto(Guid contentId, ModerateContentRequest body, CancellationToken ct)
    {
        await moderationService.ModeratePhotoAsync(contentId, body, ct);
        return NoContent();
    }
}
