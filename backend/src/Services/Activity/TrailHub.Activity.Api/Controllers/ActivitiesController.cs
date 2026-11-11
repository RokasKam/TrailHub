using System.Text;
using Microsoft.AspNetCore.Mvc;
using TrailHub.Activity.Api.Requests;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;

namespace TrailHub.Activity.Api.Controllers;

/// <summary>Recorded and imported hikes, GPX export and hiking statistics.</summary>
[Route("api/activities")]
public sealed class ActivitiesController(IActivityService activityService) : ApiController
{
    /// <summary>The caller's hikes, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ActivitySummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ActivitySummaryResponse>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await activityService.GetActivitiesAsync(new PageRequest(page ?? 1, pageSize ?? 20), ct));

    /// <summary>All-time and this-year totals plus a 12-month breakdown.</summary>
    [HttpGet("statistics")]
    [ProducesResponseType<HikingStatisticsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HikingStatisticsResponse>> Statistics(CancellationToken ct) =>
        Ok(await activityService.GetStatisticsAsync(ct));

    /// <summary>A hike with its track (one list of points per pause/resume segment) and elevation profile.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ActivityDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDetailsResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await activityService.GetActivityAsync(id, ct));

    /// <summary>Downloads the hike as GPX 1.1 with timestamps; each segment becomes a <c>&lt;trkseg&gt;</c>.</summary>
    [HttpGet("{id:guid}/gpx")]
    [Produces("application/gpx+xml")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportGpx(Guid id, CancellationToken ct)
    {
        var file = await activityService.ExportGpxAsync(id, ct);
        return File(Encoding.UTF8.GetBytes(file.Content), "application/gpx+xml", file.FileName);
    }

    /// <summary>Uploads a finished recording. The id is generated on the device.</summary>
    /// <remarks>
    /// Idempotent: re-sending the same id returns the stored hike instead of creating a duplicate, so an
    /// offline sync queue can retry safely. An id already used by another user returns 409.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [ProducesResponseType<ActivitySummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivitySummaryResponse>> Record(Guid id, RecordActivityRequest body, CancellationToken ct) =>
        Ok(await activityService.RecordAsync(id, body, ct));

    /// <summary>Renames a hike or edits its notes.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateActivityRequest body, CancellationToken ct)
    {
        await activityService.UpdateAsync(id, body, ct);
        return NoContent();
    }

    /// <summary>Deletes a hike; profile totals are updated through an integration event.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await activityService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Imports a hike from a GPX file. Every track point needs a <c>&lt;time&gt;</c>.</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType<ActivitySummaryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActivitySummaryResponse>> Import([FromForm] ImportActivityForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        var activity = await activityService.ImportGpxAsync(stream, form.Name, form.TrailId, ct);
        return CreatedAtAction(nameof(Get), new { id = activity.Id }, activity);
    }
}
