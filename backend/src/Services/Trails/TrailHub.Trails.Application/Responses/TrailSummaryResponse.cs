using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Responses;

public sealed record TrailSummaryResponse(
    Guid Id,
    string Name,
    string Region,
    Difficulty Difficulty,
    double DistanceMeters,
    double ElevationGainMeters,
    int EstimatedDurationMinutes,
    bool IsLoop,
    LocationResponse Start,
    double AverageRating,
    int ReviewCount,
    double? DistanceFromSearchPointMeters);
