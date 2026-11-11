using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Responses;

public sealed record TrailDetailsResponse(
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
    LocationResponse Start,
    GeoBounds Bounds,
    double AverageRating,
    int ReviewCount,
    bool IsFavorite,
    string CreatedBy,
    DateTime CreatedAtUtc,
    IReadOnlyList<PointOfInterestResponse> PointsOfInterest,
    IReadOnlyList<TrailPhotoResponse> Photos,
    ConditionReportResponse? LatestCondition,
    HiddenBy? HiddenBy,
    bool ViewerIsModerator);
