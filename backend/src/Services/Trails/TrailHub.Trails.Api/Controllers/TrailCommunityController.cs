using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trails.Application.Community;
using TrailHub.Trails.Application.Trails;
using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Api.Controllers;

/// <summary>Favorites, reviews and trail condition reports.</summary>
[Route("api/trails")]
public sealed class TrailCommunityController : ApiController
{
    /// <summary>The caller's favorite trails, most recently saved first.</summary>
    [HttpGet("favorites")]
    [ProducesResponseType<IReadOnlyList<TrailSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrailSummaryDto>>> GetFavorites([FromServices] IQueryHandler<GetFavoriteTrailsQuery, IReadOnlyList<TrailSummaryDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetFavoriteTrailsQuery(), ct));

    /// <summary>Saves a trail to favorites. Idempotent.</summary>
    [HttpPut("{id:guid}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddFavorite(Guid id, [FromServices] ICommandHandler<SetFavoriteCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new SetFavoriteCommand(id, IsFavorite: true), ct));

    /// <summary>Removes a trail from favorites. Idempotent.</summary>
    [HttpDelete("{id:guid}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveFavorite(Guid id, [FromServices] ICommandHandler<SetFavoriteCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new SetFavoriteCommand(id, IsFavorite: false), ct));

    /// <summary>Reports a trail as inaccurate, dangerous, inappropriate or spam. Reporting again updates your report.</summary>
    /// <remarks>After 3 people report a trail it's taken out of Discover until a moderator reviews it.</remarks>
    [HttpPost("{id:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Report(Guid id, ReportTrailRequest body, [FromServices] ICommandHandler<ReportTrailCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new ReportTrailCommand(id, body.Reason, body.Details), ct));

    /// <summary>Moderators: trails with open reports, and trails hidden by reports or moderators.</summary>
    [HttpGet("moderation")]
    [ProducesResponseType<IReadOnlyList<ModerationItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ModerationItemDto>>> ModerationQueue([FromServices] IQueryHandler<GetModerationQueueQuery, IReadOnlyList<ModerationItemDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetModerationQueueQuery(), ct));

    /// <summary>Moderators: hide a trail from Discover (<c>hidden: true</c>) or restore it. Resolves its open reports.</summary>
    [HttpPut("{id:guid}/moderation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Moderate(Guid id, ModerateTrailRequest body, [FromServices] ICommandHandler<ModerateTrailCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new ModerateTrailCommand(id, body.Hidden), ct));

    /// <summary>Reviews for a trail, newest first.</summary>
    [HttpGet("{id:guid}/reviews")]
    [ProducesResponseType<PagedResult<ReviewDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetReviews(
        Guid id,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] IQueryHandler<GetReviewsQuery, PagedResult<ReviewDto>> handler,
        CancellationToken ct) =>
        FromResult(await handler.Handle(new GetReviewsQuery(id, new PageRequest(page ?? 1, pageSize ?? 20)), ct));

    /// <summary>Creates or updates the caller's review (one per user per trail) and refreshes the trail's rating.</summary>
    [HttpPut("{id:guid}/reviews/mine")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> UpsertReview(Guid id, ReviewRequest body, [FromServices] ICommandHandler<UpsertReviewCommand, ReviewDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new UpsertReviewCommand(id, body.Rating, body.Comment), ct));

    /// <summary>Recent condition reports (mud, snow, closures…), newest first.</summary>
    [HttpGet("{id:guid}/conditions")]
    [ProducesResponseType<IReadOnlyList<ConditionReportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConditionReportDto>>> GetConditions(Guid id, [FromServices] IQueryHandler<GetConditionReportsQuery, IReadOnlyList<ConditionReportDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetConditionReportsQuery(id), ct));

    /// <summary>Reports current conditions on a trail.</summary>
    [HttpPost("{id:guid}/conditions")]
    [ProducesResponseType<ConditionReportDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConditionReportDto>> ReportCondition(
        Guid id,
        ConditionReportRequest body,
        [FromServices] ICommandHandler<ReportConditionCommand, ConditionReportDto> handler,
        CancellationToken ct) =>
        FromResult(await handler.Handle(new ReportConditionCommand(id, body.Condition, body.Note), ct), report => CreatedAtAction(nameof(GetConditions), new { id }, report));
}

/// <param name="Rating">1–5 stars.</param>
/// <param name="Comment">Optional, up to 4000 characters.</param>
public sealed record ReviewRequest(int Rating, string? Comment);

public sealed record ConditionReportRequest(TrailCondition Condition, string? Note);

public sealed record ReportTrailRequest(ReportReason Reason, string? Details);

public sealed record ModerateTrailRequest(bool Hidden);
