using System.Text;
using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trails.Application.Trails;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Api.Controllers;

/// <summary>Trail discovery, details, GPX import/export and points of interest.</summary>
[Route("api/trails")]
public sealed class TrailsController : ApiController
{
    private const long MaxGpxUploadBytes = 20 * 1024 * 1024;

    /// <summary>Searches trails. All filters are optional and combined.</summary>
    /// <remarks>
    /// <c>lat</c>/<c>lon</c> (+ <c>radiusKm</c>, default 50) finds trails starting nearby and enables distance sorting.
    /// <c>minLat</c>/<c>minLon</c>/<c>maxLat</c>/<c>maxLon</c> returns trails crossing the visible map area.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<TrailSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TrailSummaryDto>>> Search(
        [FromQuery] TrailSearchRequest request,
        [FromServices] IQueryHandler<SearchTrailsQuery, PagedResult<TrailSummaryDto>> handler,
        CancellationToken ct) =>
        FromResult(await handler.Handle(new SearchTrailsQuery(request.ToCriteria(), new PageRequest(request.Page ?? 1, request.PageSize ?? 20)), ct));

    /// <summary>Trail details including points of interest, photos and the latest condition report.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TrailDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrailDetailsDto>> Get(Guid id, [FromServices] IQueryHandler<GetTrailDetailsQuery, TrailDetailsDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetTrailDetailsQuery(id), ct));

    /// <summary>Route points for drawing the trail on a map.</summary>
    /// <param name="id">Trail id.</param>
    /// <param name="maxPoints">Downsample to at most this many points (keeps both ends). Full resolution when omitted.</param>
    [HttpGet("{id:guid}/route")]
    [ProducesResponseType<TrailRouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrailRouteDto>> GetRoute(Guid id, [FromQuery] int? maxPoints, [FromServices] IQueryHandler<GetTrailRouteQuery, TrailRouteDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetTrailRouteQuery(id, maxPoints), ct));

    /// <summary>Distance-vs-elevation samples for an elevation chart.</summary>
    /// <param name="id">Trail id.</param>
    /// <param name="samples">Number of samples, 2–2000 (default 200).</param>
    [HttpGet("{id:guid}/elevation-profile")]
    [ProducesResponseType<IReadOnlyList<ElevationSample>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ElevationSample>>> GetElevationProfile(
        Guid id,
        [FromQuery] int? samples,
        [FromServices] IQueryHandler<GetElevationProfileQuery, IReadOnlyList<ElevationSample>> handler,
        CancellationToken ct) =>
        FromResult(await handler.Handle(new GetElevationProfileQuery(id, samples ?? 200), ct));

    /// <summary>Downloads the trail as a GPX 1.1 file (track + points of interest as waypoints).</summary>
    [HttpGet("{id:guid}/gpx")]
    [Produces("application/gpx+xml")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportGpx(Guid id, [FromServices] IQueryHandler<ExportTrailGpxQuery, GpxFileDto> handler, CancellationToken ct)
    {
        var result = await handler.Handle(new ExportTrailGpxQuery(id), ct);
        return result.IsSuccess
            ? File(Encoding.UTF8.GetBytes(result.Value.Content), "application/gpx+xml", result.Value.FileName)
            : ProblemFor(result.Error!);
    }

    /// <summary>Creates a trail from a GPX file. Waypoints become points of interest.</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxGpxUploadBytes)]
    [ProducesResponseType<CreatedTrail>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatedTrail>> Import(
        [FromForm] ImportTrailForm form,
        [FromServices] ICommandHandler<ImportTrailFromGpxCommand, Guid> handler,
        CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        var result = await handler.Handle(new ImportTrailFromGpxCommand(stream, form.Name, form.Description, form.Region, form.Difficulty), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value }, new CreatedTrail(result.Value))
            : ProblemFor(result.Error!);
    }

    /// <summary>Updates name, description, region and difficulty. Only the trail's creator can do this.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateTrailRequest body, [FromServices] ICommandHandler<UpdateTrailCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new UpdateTrailCommand(id, body.Name, body.Description, body.Region, body.Difficulty), ct));

    /// <summary>Adds a point of interest (water source, shelter, campsite, viewpoint…). Any signed-in hiker can add one.</summary>
    [HttpPost("{id:guid}/points-of-interest")]
    [ProducesResponseType<PointOfInterestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PointOfInterestDto>> AddPointOfInterest(
        Guid id,
        AddPointOfInterestRequest body,
        [FromServices] ICommandHandler<AddPointOfInterestCommand, PointOfInterestDto> handler,
        CancellationToken ct) =>
        FromResult(
            await handler.Handle(new AddPointOfInterestCommand(id, body.Name, body.Type, body.Latitude, body.Longitude, body.Elevation, body.Description), ct),
            poi => CreatedAtAction(nameof(Get), new { id }, poi));

    /// <summary>Removes a point of interest. Only the trail's creator can do this.</summary>
    [HttpDelete("{id:guid}/points-of-interest/{poiId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePointOfInterest(Guid id, Guid poiId, [FromServices] ICommandHandler<RemovePointOfInterestCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new RemovePointOfInterestCommand(id, poiId), ct));

    /// <summary>Uploads a photo (JPEG, PNG, WebP or HEIC, up to 10 MB). Any signed-in hiker can add one.</summary>
    [HttpPost("{id:guid}/photos/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType<TrailPhotoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrailPhotoDto>> UploadPhoto(
        Guid id,
        [FromForm] UploadPhotoForm form,
        [FromServices] ICommandHandler<UploadTrailPhotoCommand, TrailPhotoDto> handler,
        CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        return FromResult(
            await handler.Handle(new UploadTrailPhotoCommand(id, stream, form.File.ContentType, form.File.Length, form.Caption), ct),
            photo => CreatedAtAction(nameof(Get), new { id }, photo));
    }

    /// <summary>Attaches a photo hosted elsewhere, by its https URL.</summary>
    [HttpPost("{id:guid}/photos")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddPhoto(Guid id, AddPhotoRequest body, [FromServices] ICommandHandler<AddTrailPhotoCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new AddTrailPhotoCommand(id, body.Url, body.Caption), ct));
}

/// <summary>Query string for <c>GET /api/trails</c>.</summary>
public sealed record TrailSearchRequest
{
    /// <summary>Text matched against trail name and region.</summary>
    public string? Q { get; init; }

    public double? Lat { get; init; }

    public double? Lon { get; init; }

    /// <summary>Search radius around <c>lat</c>/<c>lon</c>, in km (default 50, max 500).</summary>
    public double? RadiusKm { get; init; }

    public double? MinLat { get; init; }

    public double? MinLon { get; init; }

    public double? MaxLat { get; init; }

    public double? MaxLon { get; init; }

    public double? MinDistanceKm { get; init; }

    public double? MaxDistanceKm { get; init; }

    /// <summary>Repeat to allow several, e.g. <c>difficulty=Easy&amp;difficulty=Moderate</c>.</summary>
    public Difficulty[]? Difficulty { get; init; }

    public double? MinElevationGain { get; init; }

    public double? MaxElevationGain { get; init; }

    public int? MinDurationMinutes { get; init; }

    public int? MaxDurationMinutes { get; init; }

    public TrailSort? Sort { get; init; }

    public int? Page { get; init; }

    /// <summary>1–100, default 20.</summary>
    public int? PageSize { get; init; }

    internal TrailSearchCriteria ToCriteria() => new(
        Q,
        Lat,
        Lon,
        RadiusKm ?? (Lat is null ? null : 50),
        MinLat is { } a && MinLon is { } b && MaxLat is { } c && MaxLon is { } d ? new GeoBounds(a, b, c, d) : null,
        MinDistanceKm,
        MaxDistanceKm,
        Difficulty,
        MinElevationGain,
        MaxElevationGain,
        MinDurationMinutes,
        MaxDurationMinutes,
        Sort ?? TrailSort.Relevance);
}

/// <summary>Multipart form for <c>POST /api/trails/import</c>.</summary>
public sealed record ImportTrailForm
{
    /// <summary>A .gpx file (GPX 1.0 or 1.1; tracks or routes).</summary>
    public required IFormFile File { get; init; }

    /// <summary>Human-readable location, e.g. "High Tatras, Slovakia".</summary>
    public required string Region { get; init; }

    public required Difficulty Difficulty { get; init; }

    /// <summary>Defaults to the name in the GPX file.</summary>
    public string? Name { get; init; }

    public string? Description { get; init; }
}

public sealed record CreatedTrail(Guid Id);

/// <summary>Multipart form for <c>POST /api/trails/{id}/photos/upload</c>.</summary>
public sealed record UploadPhotoForm
{
    public required IFormFile File { get; init; }

    public string? Caption { get; init; }
}

public sealed record UpdateTrailRequest(string Name, string? Description, string Region, Difficulty Difficulty);

public sealed record AddPointOfInterestRequest(string Name, PointOfInterestType Type, double Latitude, double Longitude, double? Elevation, string? Description);

public sealed record AddPhotoRequest(string Url, string? Caption);
