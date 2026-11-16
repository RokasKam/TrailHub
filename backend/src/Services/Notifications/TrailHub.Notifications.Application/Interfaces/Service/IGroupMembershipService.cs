using TrailHub.Notifications.Application.Requests;

namespace TrailHub.Notifications.Application.Interfaces.Service;

/// <summary>Keeps this service's copy of group memberships current.</summary>
public interface IGroupMembershipService
{
    /// <summary>Applies the snapshot (unless it's older than the stored one) and tells members about people who joined.</summary>
    Task SyncGroupMembersAsync(SyncGroupMembersRequest request, CancellationToken cancellationToken);
}
