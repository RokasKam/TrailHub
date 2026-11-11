namespace TrailHub.Routes.Domain.Routes;

/// <summary>Distance, climbing and walking time for part of a track.</summary>
/// <param name="MaxSacScale">Hardest trail grade on it: 1 = T1 hiking … 6 = T6 difficult alpine; 0 = unknown.</param>
public sealed record TrackStats(
    double DistanceMeters,
    double AscentMeters,
    double DescentMeters,
    double? MinElevationMeters,
    double? MaxElevationMeters,
    int DurationMinutes,
    int MaxSacScale);
