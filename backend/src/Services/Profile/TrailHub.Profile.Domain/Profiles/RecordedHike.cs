namespace TrailHub.Profile.Domain.Profiles;

/// <summary>A hike counted towards this profile's totals. Also makes event handling idempotent.</summary>
public sealed record RecordedHike(Guid ActivityId, double DistanceMeters, double ElevationGainMeters, DateTime CompletedAtUtc);
