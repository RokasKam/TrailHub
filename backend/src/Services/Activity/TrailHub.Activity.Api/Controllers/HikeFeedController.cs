using Microsoft.AspNetCore.Mvc;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;

namespace TrailHub.Activity.Api.Controllers;

/// <summary>Hikes shared with the caller's groups, with likes and comments.</summary>
[Route("api/activities/feed")]
public sealed class HikeFeedController(IHikeFeedService feedService, IContentModerationService moderationService) : ApiController
{
    /// <summary>Hikes shared with the caller's groups (or one of them) and their own shared hikes, newest share first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<FeedPostResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<FeedPostResponse>>> List([FromQuery] Guid? groupId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await feedService.GetFeedAsync(groupId, new PageRequest(page ?? 1, pageSize ?? 20), ct));

    /// <summary>A shared hike with its comments (oldest first) and elevation profile.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<FeedPostDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedPostDetailsResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await feedService.GetPostAsync(id, ct));

    /// <summary>Likes a shared hike. Idempotent.</summary>
    [HttpPut("{id:guid}/like")]
    [ProducesResponseType<LikeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LikeResponse>> Like(Guid id, CancellationToken ct) =>
        Ok(await feedService.LikeAsync(id, ct));

    /// <summary>Removes the caller's like. Idempotent.</summary>
    [HttpDelete("{id:guid}/like")]
    [ProducesResponseType<LikeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LikeResponse>> Unlike(Guid id, CancellationToken ct) =>
        Ok(await feedService.UnlikeAsync(id, ct));

    /// <summary>Comments on a shared hike (1 to 1000 characters).</summary>
    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentResponse>> AddComment(Guid id, AddCommentRequest body, CancellationToken ct)
    {
        var comment = await feedService.AddCommentAsync(id, body, ct);
        return CreatedAtAction(nameof(Get), new { id }, comment);
    }

    /// <summary>Deletes a comment. Allowed for its author, the hike's author and moderators.</summary>
    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        await feedService.DeleteCommentAsync(id, commentId, ct);
        return NoContent();
    }

    /// <summary>Reports a comment as inappropriate, spam or harassment. Reporting again updates your report.</summary>
    /// <remarks>After 3 people report a comment it's hidden until a moderator reviews it.</remarks>
    [HttpPost("{id:guid}/comments/{commentId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReportComment(Guid id, Guid commentId, ReportContentRequest body, CancellationToken ct)
    {
        await moderationService.ReportCommentAsync(id, commentId, body, ct);
        return NoContent();
    }

    /// <summary>Reports a hike photo as inappropriate, spam or harassment. Reporting again updates your report.</summary>
    /// <remarks>After 3 people report a photo it's hidden until a moderator reviews it.</remarks>
    [HttpPost("{id:guid}/photos/{photoId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReportPhoto(Guid id, Guid photoId, ReportContentRequest body, CancellationToken ct)
    {
        await moderationService.ReportPhotoAsync(id, photoId, body, ct);
        return NoContent();
    }
}
