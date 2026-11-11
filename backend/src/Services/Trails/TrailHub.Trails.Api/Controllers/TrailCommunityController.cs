using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Api.Controllers;

/// <summary>Favorites, reviews, trail condition reports, and reporting and moderating trails and their photos.</summary>
[Route("api/trails")]
public sealed class TrailCommunityController(
    IFavoriteTrailService favoriteService,
    IReviewService reviewService,
    IConditionReportService conditionService,
    IModerationService moderationService,
    IPhotoModerationService photoModerationService) : ApiController
{
    /// <summary>The caller's favorite trails, most recently saved first.</summary>
    [HttpGet("favorites")]
    [ProducesResponseType<IReadOnlyList<TrailSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrailSummaryResponse>>> GetFavorites(CancellationToken ct) =>
        Ok(await favoriteService.GetFavoritesAsync(ct));

    /// <summary>Saves a trail to favorites. Idempotent.</summary>
    [HttpPut("{id:guid}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddFavorite(Guid id, CancellationToken ct)
    {
        await favoriteService.SetFavoriteAsync(id, isFavorite: true, ct);
        return NoContent();
    }

    /// <summary>Removes a trail from favorites. Idempotent.</summary>
    [HttpDelete("{id:guid}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveFavorite(Guid id, CancellationToken ct)
    {
        await favoriteService.SetFavoriteAsync(id, isFavorite: false, ct);
        return NoContent();
    }

    /// <summary>Reports a trail as inaccurate, dangerous, inappropriate or spam. Reporting again updates your report.</summary>
    /// <remarks>After 3 people report a trail it's taken out of Discover until a moderator reviews it.</remarks>
    [HttpPost("{id:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Report(Guid id, ReportTrailRequest body, CancellationToken ct)
    {
        await moderationService.ReportAsync(id, body, ct);
        return NoContent();
    }

    /// <summary>Moderators: trails with open reports, and trails hidden by reports or moderators.</summary>
    [HttpGet("moderation")]
    [ProducesResponseType<IReadOnlyList<ModerationItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ModerationItemResponse>>> ModerationQueue(CancellationToken ct) =>
        Ok(await moderationService.GetQueueAsync(ct));

    /// <summary>Moderators: hide a trail from Discover (<c>hidden: true</c>) or restore it. Resolves its open reports.</summary>
    [HttpPut("{id:guid}/moderation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Moderate(Guid id, ModerateTrailRequest body, CancellationToken ct)
    {
        await moderationService.ModerateAsync(id, body, ct);
        return NoContent();
    }

    /// <summary>Reports a trail photo as inappropriate, spam or harassment. Reporting again updates your report.</summary>
    /// <remarks>After 3 people report a photo it's hidden until a moderator reviews it.</remarks>
    [HttpPost("{id:guid}/photos/{photoId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReportPhoto(Guid id, Guid photoId, ReportContentRequest body, CancellationToken ct)
    {
        await photoModerationService.ReportAsync(id, photoId, body, ct);
        return NoContent();
    }

    /// <summary>Moderators: trail photos with open reports, and hidden photos, most reported first.</summary>
    [HttpGet("moderation/photos")]
    [ProducesResponseType<IReadOnlyList<PhotoModerationItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PhotoModerationItemResponse>>> PhotoModerationQueue(CancellationToken ct) =>
        Ok(await photoModerationService.GetQueueAsync(ct));

    /// <summary>Moderators: restore, hide or delete a trail photo. Resolves its open reports.</summary>
    [HttpPut("{id:guid}/photos/{photoId:guid}/moderation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModeratePhoto(Guid id, Guid photoId, ModerateContentRequest body, CancellationToken ct)
    {
        await photoModerationService.ModerateAsync(id, photoId, body, ct);
        return NoContent();
    }

    /// <summary>Reviews for a trail, newest first.</summary>
    [HttpGet("{id:guid}/reviews")]
    [ProducesResponseType<PagedResult<ReviewResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewResponse>>> GetReviews(Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await reviewService.GetReviewsAsync(id, new PageRequest(page ?? 1, pageSize ?? 20), ct));

    /// <summary>Creates or updates the caller's review (one per user per trail) and refreshes the trail's rating.</summary>
    [HttpPut("{id:guid}/reviews/mine")]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> UpsertReview(Guid id, ReviewRequest body, CancellationToken ct) =>
        Ok(await reviewService.UpsertMineAsync(id, body, ct));

    /// <summary>Recent condition reports (mud, snow, closures…), newest first.</summary>
    [HttpGet("{id:guid}/conditions")]
    [ProducesResponseType<IReadOnlyList<ConditionReportResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConditionReportResponse>>> GetConditions(Guid id, CancellationToken ct) =>
        Ok(await conditionService.GetRecentAsync(id, 20, ct));

    /// <summary>Reports current conditions on a trail.</summary>
    [HttpPost("{id:guid}/conditions")]
    [ProducesResponseType<ConditionReportResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConditionReportResponse>> ReportCondition(Guid id, ConditionReportRequest body, CancellationToken ct)
    {
        var report = await conditionService.ReportAsync(id, body, ct);
        return CreatedAtAction(nameof(GetConditions), new { id }, report);
    }
}
