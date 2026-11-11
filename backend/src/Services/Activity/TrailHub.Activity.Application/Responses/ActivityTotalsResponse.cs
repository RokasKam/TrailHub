namespace TrailHub.Activity.Application.Responses;

public sealed record ActivityTotalsResponse(
    int Hikes,
    double DistanceMeters,
    double ElevationGainMeters,
    long MovingDurationSeconds,
    double LongestHikeMeters,
    double? HighestElevationMeters);
