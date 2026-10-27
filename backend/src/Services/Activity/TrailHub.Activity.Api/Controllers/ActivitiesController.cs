using System.Text;
using Microsoft.AspNetCore.Mvc;
using TrailHub.Activity.Application.Activities;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;

namespace TrailHub.Activity.Api.Controllers;

/// <summary>Recorded and imported hikes, GPX export and hiking statistics.</summary>
[Route("api/activities")]
public sealed class ActivitiesController : ApiController
{
    /// <summary>The caller's hikes, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ActivitySummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ActivitySummaryDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] IQueryHandler<GetActivitiesQuery, PagedResult<ActivitySummaryDto>> handler,
        CancellationToken ct) =>
        FromResult(await handler.Handle(new GetActivitiesQuery(new PageRequest(page ?? 1, pageSize ?? 20)), ct));

    /// <summary>All-time and this-year totals plus a 12-month breakdown.</summary>
    [HttpGet("statistics")]
    [ProducesResponseType<HikingStatisticsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HikingStatisticsDto>> Statistics([FromServices] IQueryHandler<GetStatisticsQuery, HikingStatisticsDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetStatisticsQuery(), ct));

    /// <summary>A hike with its track (one list of points per pause/resume segment) and elevation profile.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ActivityDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDetailsDto>> Get(Guid id, [FromServices] IQueryHandler<GetActivityQuery, ActivityDetailsDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetActivityQuery(id), ct));

    /// <summary>Downloads the hike as GPX 1.1 with timestamps; each segment becomes a <c>&lt;trkseg&gt;</c>.</summary>
    [HttpGet("{id:guid}/gpx")]
    [Produces("application/gpx+xml")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportGpx(Guid id, [FromServices] IQueryHandler<ExportActivityGpxQuery, (string FileName, string Content)> handler, CancellationToken ct)
    {
        var result = await handler.Handle(new ExportActivityGpxQuery(id), ct);
        return result.IsSuccess
            ? File(Encoding.UTF8.GetBytes(result.Value.Content), "application/gpx+xml", result.Value.FileName)
            : ProblemFor(result.Error!);
    }

    /// <summary>Uploads a finished recording. The id is generated on the device.</summary>
    /// <remarks>
    /// Idempotent: re-sending the same id returns the stored hike instead of creating a duplicate, so an
    /// offline sync queue can retry safely. An id already used by another user returns 409.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [ProducesResponseType<ActivitySummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivitySummaryDto>> Record(
        Guid id,
        RecordActivityRequest body,
        [FromServices] ICommandHandler<RecordActivityCommand, ActivitySummaryDto> handler,
        CancellationToken ct) =>
        FromResult(await handler.Handle(new RecordActivityCommand(id, body.Name, body.TrailId, body.Segments, body.Notes), ct));

    /// <summary>Renames a hike or edits its notes.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateActivityRequest body, [FromServices] ICommandHandler<UpdateActivityCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new UpdateActivityCommand(id, body.Name, body.Notes), ct));

    /// <summary>Deletes a hike; profile totals are updated through an integration event.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] ICommandHandler<DeleteActivityCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new DeleteActivityCommand(id), ct));

    /// <summary>Imports a hike from a GPX file. Every track point needs a <c>&lt;time&gt;</c>.</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType<ActivitySummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActivitySummaryDto>> Import(
        [FromForm] ImportActivityForm form,
        [FromServices] ICommandHandler<ImportActivityFromGpxCommand, ActivitySummaryDto> handler,
        CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        return FromResult(
            await handler.Handle(new ImportActivityFromGpxCommand(stream, form.Name, form.TrailId), ct),
            a => CreatedAtAction(nameof(Get), new { id = a.Id }, a));
    }
}

/// <param name="Name">Hike name.</param>
/// <param name="TrailId">The trail this hike followed, if any.</param>
/// <param name="Segments">Track points per recording segment (a pause ends a segment).</param>
/// <param name="Notes">Optional notes.</param>
public sealed record RecordActivityRequest(string Name, Guid? TrailId, IReadOnlyList<IReadOnlyList<TrackPointDto>> Segments, string? Notes);

public sealed record UpdateActivityRequest(string Name, string? Notes);

/// <summary>Multipart form for <c>POST /api/activities/import</c>.</summary>
public sealed record ImportActivityForm
{
    /// <summary>A .gpx file whose track points all have timestamps.</summary>
    public required IFormFile File { get; init; }

    /// <summary>Defaults to the name in the GPX file.</summary>
    public string? Name { get; init; }

    public Guid? TrailId { get; init; }
}
