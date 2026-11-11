using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Api.Requests;

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
