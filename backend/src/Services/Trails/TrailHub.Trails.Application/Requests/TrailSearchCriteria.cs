using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Requests;

/// <summary>
/// All filters are optional and combined with AND. <c>Near*</c> + <c>RadiusKm</c> finds trails whose
/// start lies within the radius; the <c>Bounds*</c> box is for "trails visible on the map".
/// </summary>
public sealed record TrailSearchCriteria(
    string? Query = null,
    double? NearLatitude = null,
    double? NearLongitude = null,
    double? RadiusKm = null,
    GeoBounds? Bounds = null,
    double? MinDistanceKm = null,
    double? MaxDistanceKm = null,
    IReadOnlyList<Difficulty>? Difficulties = null,
    double? MinElevationGainMeters = null,
    double? MaxElevationGainMeters = null,
    int? MinDurationMinutes = null,
    int? MaxDurationMinutes = null,
    TrailSort Sort = TrailSort.Relevance);
