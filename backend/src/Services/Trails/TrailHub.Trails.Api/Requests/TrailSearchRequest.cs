using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Api.Requests;

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
