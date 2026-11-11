using TrailHub.Routes.Application.Models;

namespace TrailHub.Routes.Application.Interfaces.Repository;

/// <summary>
/// This service's copy of group memberships, kept current by events from the Groups service, to decide who can
/// see a route shared with a group. (Services don't share databases; Trips keeps its own copy the same way.)
/// </summary>
public interface IGroupDirectory
{
    Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GroupInfo>> GetForMemberAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Stores the snapshot unless a newer one is already stored.</summary>
    Task ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken);
}
