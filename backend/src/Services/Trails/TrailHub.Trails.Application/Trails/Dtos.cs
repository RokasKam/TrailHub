using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Trails;

public enum TrailSort
{
    Relevance,
    Distance,
    Rating,
    Newest,
    Length,
}

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

public sealed record LocationDto(double Latitude, double Longitude);

public sealed record TrailSummaryDto(
    Guid Id,
    string Name,
    string Region,
    Difficulty Difficulty,
    double DistanceMeters,
    double ElevationGainMeters,
    int EstimatedDurationMinutes,
    bool IsLoop,
    LocationDto Start,
    double AverageRating,
    int ReviewCount,
    double? DistanceFromSearchPointMeters);

public sealed record PointOfInterestDto(Guid Id, string Name, PointOfInterestType Type, double Latitude, double Longitude, double? Elevation, string? Description);

public sealed record TrailPhotoDto(string Url, string? Caption, DateTime UploadedAtUtc);

public sealed record ConditionReportDto(Guid Id, TrailCondition Condition, string? Note, string UserId, DateTime ReportedAtUtc);

public sealed record TrailDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    string Region,
    Difficulty Difficulty,
    double DistanceMeters,
    double ElevationGainMeters,
    double ElevationLossMeters,
    double? MaxElevationMeters,
    int EstimatedDurationMinutes,
    bool IsLoop,
    LocationDto Start,
    GeoBounds Bounds,
    double AverageRating,
    int ReviewCount,
    bool IsFavorite,
    string CreatedBy,
    DateTime CreatedAtUtc,
    IReadOnlyList<PointOfInterestDto> PointsOfInterest,
    IReadOnlyList<TrailPhotoDto> Photos,
    ConditionReportDto? LatestCondition);

public sealed record RoutePointDto(double Latitude, double Longitude, double? Elevation);

public sealed record TrailRouteDto(Guid TrailId, int TotalPoints, IReadOnlyList<RoutePointDto> Points);

public sealed record GpxFileDto(string FileName, string Content);

internal static class TrailMappings
{
    public static PointOfInterestDto ToDto(this PointOfInterest p) =>
        new(p.Id, p.Name, p.Type, p.Location.Latitude, p.Location.Longitude, p.Location.Elevation, p.Description);

    public static ConditionReportDto ToDto(this ConditionReport r) => new(r.Id, r.Condition, r.Note, r.UserId, r.ReportedAtUtc);

    public static string ToFileName(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
        return $"{(safe.Length == 0 ? "route" : safe)}.gpx";
    }
}
