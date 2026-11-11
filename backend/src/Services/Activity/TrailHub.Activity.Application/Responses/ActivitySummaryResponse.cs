using TrailHub.Activity.Domain.Activities;

namespace TrailHub.Activity.Application.Responses;

public sealed record ActivitySummaryResponse(
    Guid Id,
    string Name,
    Guid? TrailId,
    ActivitySource Source,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    double DistanceMeters,
    double ElevationGainMeters,
    long MovingDurationSeconds,
    double AverageSpeedMetersPerSecond);
