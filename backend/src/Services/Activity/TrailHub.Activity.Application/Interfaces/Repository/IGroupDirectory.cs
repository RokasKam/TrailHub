using TrailHub.Activity.Application.Models;

namespace TrailHub.Activity.Application.Interfaces.Repository;

/// <summary>
/// This service's copy of group memberships, kept current by events from the Groups service, to decide who sees a
/// shared hike. (Services don't share databases; Trips and Routes keep their own copies the same way.)
/// </summary>
public interface IGroupDirectory
{
    /// <summary>The groups <paramref name="userId"/> currently belongs to, with their members.</summary>
    Task<IReadOnlyList<GroupInfo>> GetForMemberAsync(string userId, CancellationToken cancellationToken);

    /// <summary>A display name for each user found in any group (for people no longer in the caller's groups).</summary>
    Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);

    /// <summary>Stores the snapshot unless a newer one is already stored.</summary>
    Task ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken);
}
