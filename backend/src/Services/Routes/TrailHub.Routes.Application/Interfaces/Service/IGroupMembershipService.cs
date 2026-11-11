using TrailHub.Routes.Application.Models;

namespace TrailHub.Routes.Application.Interfaces.Service;

/// <summary>Keeps this service's copy of group memberships current from the Groups service's events.</summary>
public interface IGroupMembershipService
{
    /// <summary>Stores a group's latest name and members (or its deletion) unless a newer snapshot is already stored.</summary>
    Task SyncAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken);
}
