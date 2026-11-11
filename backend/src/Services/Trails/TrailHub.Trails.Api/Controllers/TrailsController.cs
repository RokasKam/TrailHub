using System.Text;
using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trails.Api.Requests;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Api.Controllers;

/// <summary>Trail discovery, details, GPX import/export and points of interest.</summary>
[Route("api/trails")]
public sealed class TrailsController(
    ITrailService trailService,
    IPointOfInterestService pointOfInterestService,
    ITrailPhotoService photoService) : ApiController
{
    private const long MaxGpxUploadBytes = 20 * 1024 * 1024;

    /// <summary>Searches trails. All filters are optional and combined.</summary>
    /// <remarks>
    /// <c>lat</c>/<c>lon</c> (+ <c>radiusKm</c>, default 50) finds trails starting nearby and enables distance sorting.
    /// <c>minLat</c>/<c>minLon</c>/<c>maxLat</c>/<c>maxLon</c> returns trails crossing the visible map area.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<TrailSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TrailSummaryResponse>>> Search([FromQuery] TrailSearchRequest request, CancellationToken ct) =>
        Ok(await trailService.SearchAsync(request.ToCriteria(), new PageRequest(request.Page ?? 1, request.PageSize ?? 20), ct));

    /// <summary>Trail details including points of interest, photos and the latest condition report.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TrailDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrailDetailsResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await trailService.GetTrailAsync(id, ct));

    /// <summary>Route points for drawing the trail on a map.</summary>
    /// <param name="id">Trail id.</param>
    /// <param name="maxPoints">Downsample to at most this many points (keeps both ends). Full resolution when omitted.</param>
    /// <param name="ct">Cancels the request.</param>
    [HttpGet("{id:guid}/route")]
    [ProducesResponseType<TrailRouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrailRouteResponse>> GetRoute(Guid id, [FromQuery] int? maxPoints, CancellationToken ct) =>
        Ok(await trailService.GetRouteAsync(id, maxPoints, ct));

    /// <summary>Distance-vs-elevation samples for an elevation chart.</summary>
    /// <param name="id">Trail id.</param>
    /// <param name="samples">Number of samples, 2–2000 (default 200).</param>
    /// <param name="ct">Cancels the request.</param>
    [HttpGet("{id:guid}/elevation-profile")]
    [ProducesResponseType<IReadOnlyList<ElevationSample>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ElevationSample>>> GetElevationProfile(Guid id, [FromQuery] int? samples, CancellationToken ct) =>
        Ok(await trailService.GetElevationProfileAsync(id, samples ?? 200, ct));

    /// <summary>Downloads the trail as a GPX 1.1 file (track + points of interest as waypoints).</summary>
    [HttpGet("{id:guid}/gpx")]
    [Produces("application/gpx+xml")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportGpx(Guid id, CancellationToken ct)
    {
        var file = await trailService.ExportGpxAsync(id, ct);
        return File(Encoding.UTF8.GetBytes(file.Content), "application/gpx+xml", file.FileName);
    }

    /// <summary>Creates a trail from a GPX file. Waypoints become points of interest.</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxGpxUploadBytes)]
    [ProducesResponseType<CreatedTrailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatedTrailResponse>> Import([FromForm] ImportTrailForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        var created = await trailService.ImportGpxAsync(stream, form.Name, form.Description, form.Region, form.Difficulty, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Updates name, description, region and difficulty. Only the trail's creator can do this.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateTrailRequest body, CancellationToken ct)
    {
        await trailService.UpdateAsync(id, body, ct);
        return NoContent();
    }

    /// <summary>Adds a point of interest (water source, shelter, campsite, viewpoint…). Any signed-in hiker can add one.</summary>
    [HttpPost("{id:guid}/points-of-interest")]
    [ProducesResponseType<PointOfInterestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PointOfInterestResponse>> AddPointOfInterest(Guid id, AddPointOfInterestRequest body, CancellationToken ct)
    {
        var poi = await pointOfInterestService.AddAsync(id, body, ct);
        return CreatedAtAction(nameof(Get), new { id }, poi);
    }

    /// <summary>Removes a point of interest. Only the trail's creator can do this.</summary>
    [HttpDelete("{id:guid}/points-of-interest/{poiId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePointOfInterest(Guid id, Guid poiId, CancellationToken ct)
    {
        await pointOfInterestService.RemoveAsync(id, poiId, ct);
        return NoContent();
    }

    /// <summary>Uploads a photo (JPEG, PNG, WebP or HEIC, up to 10 MB). Any signed-in hiker can add one.</summary>
    [HttpPost("{id:guid}/photos/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType<TrailPhotoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrailPhotoResponse>> UploadPhoto(Guid id, [FromForm] UploadPhotoForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        var photo = await photoService.UploadAsync(id, stream, form.File.ContentType, form.File.Length, form.Caption, ct);
        return CreatedAtAction(nameof(Get), new { id }, photo);
    }

    /// <summary>Attaches a photo hosted elsewhere, by its https URL.</summary>
    [HttpPost("{id:guid}/photos")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddPhoto(Guid id, AddPhotoRequest body, CancellationToken ct)
    {
        await photoService.AddAsync(id, body, ct);
        return NoContent();
    }
}
