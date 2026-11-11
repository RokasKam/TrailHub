using TrailHub.Trips.Application.Models;

namespace TrailHub.Trips.Application.Interfaces.Providers;

/// <summary>
/// This service's copy of group memberships, kept current by events from the Groups service
/// (services don't share databases). Used to decide who can see a shared trip.
/// </summary>
public interface IGroupDirectory
{
    Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetGroupIdsForMemberAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Stores the snapshot unless a newer one is already stored. Returns whether it was applied.</summary>
    Task<bool> ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken);
}
