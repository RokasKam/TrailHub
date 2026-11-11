using TrailHub.Trips.Application.Requests;

namespace TrailHub.Trips.Application.Interfaces.Service;

/// <summary>Keeps this service's copy of group memberships current.</summary>
public interface IGroupMembershipService
{
    /// <summary>Updates the membership copy, then fixes up trips shared with the group (claims of people who left, deleted groups).</summary>
    Task SyncGroupMembersAsync(SyncGroupMembersRequest request, CancellationToken cancellationToken);
}
