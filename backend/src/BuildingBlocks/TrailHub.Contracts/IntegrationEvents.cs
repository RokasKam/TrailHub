namespace TrailHub.Contracts;

// Integration events are the only types shared between services. Keep them flat, versionless
// and additive: consumers in other services may be running an older build.

public sealed record ActivityCompletedIntegrationEvent(
    Guid ActivityId,
    string UserId,
    Guid? TrailId,
    double DistanceMeters,
    double ElevationGainMeters,
    long MovingDurationSeconds,
    DateTime CompletedAtUtc);

public sealed record ActivityDeletedIntegrationEvent(Guid ActivityId, string UserId);

public sealed record GroupHikeScheduledIntegrationEvent(
    Guid GroupId,
    Guid HikeId,
    string Title,
    DateTime ScheduledAtUtc,
    IReadOnlyList<string> MemberUserIds);
