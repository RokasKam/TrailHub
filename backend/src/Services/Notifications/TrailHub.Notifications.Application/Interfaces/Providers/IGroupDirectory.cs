using TrailHub.Notifications.Application.Models;

namespace TrailHub.Notifications.Application.Interfaces.Providers;

/// <summary>
/// This service's copy of group memberships, kept current by events from the Groups service (services don't share
/// databases). Used to find who to tell about a shared hike and to spot people joining.
/// </summary>
public interface IGroupDirectory
{
    Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GroupInfo>> GetManyAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken);

    /// <summary>Stores the snapshot (not saved yet) unless a newer one is already stored.</summary>
    Task<SnapshotResult> ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken);
}
