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

/// <summary>
/// The full member list of a group, published whenever it changes (and for every group when the Groups
/// service starts, so new consumers can catch up). Consumers replace what they hold; ignore snapshots
/// older than the one already applied.
/// </summary>
public sealed record GroupMembersChangedIntegrationEvent(
    Guid GroupId,
    string Name,
    IReadOnlyList<GroupMemberSnapshot> Members,
    bool Deleted,
    DateTime OccurredAtUtc);

public sealed record GroupMemberSnapshot(string UserId, string DisplayName);
