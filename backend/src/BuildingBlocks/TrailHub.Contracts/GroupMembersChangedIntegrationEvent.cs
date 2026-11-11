namespace TrailHub.Contracts;

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
