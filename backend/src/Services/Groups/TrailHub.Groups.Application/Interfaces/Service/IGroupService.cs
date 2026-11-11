using TrailHub.Groups.Application.Requests;
using TrailHub.Groups.Application.Responses;

namespace TrailHub.Groups.Application.Interfaces.Service;

/// <summary>Hiking groups, their members and invite codes.</summary>
public interface IGroupService
{
    /// <summary>Groups the caller belongs to, with their next scheduled hike.</summary>
    Task<IReadOnlyList<GroupSummaryResponse>> GetMyGroupsAsync(CancellationToken cancellationToken);

    /// <summary>Members and upcoming hikes. Groups the caller isn't in are reported as not found.</summary>
    Task<GroupResponse> GetGroupAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>Creates a group; the caller becomes its owner.</summary>
    Task<GroupResponse> CreateAsync(CreateGroupRequest request, CancellationToken cancellationToken);

    /// <summary>Joins a group with its invite code (case-insensitive).</summary>
    Task<GroupResponse> JoinAsync(JoinGroupRequest request, CancellationToken cancellationToken);

    /// <summary>Leaves the group and drops the caller from its live map. The owner must transfer ownership first.</summary>
    Task LeaveAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>Removes a member and drops them from the live map (owner only).</summary>
    Task RemoveMemberAsync(Guid groupId, string userId, CancellationToken cancellationToken);

    /// <summary>Hands ownership to another member (owner only).</summary>
    Task<GroupResponse> TransferOwnershipAsync(Guid groupId, TransferOwnershipRequest request, CancellationToken cancellationToken);

    /// <summary>Issues a new invite code; the old one stops working (owner only).</summary>
    Task<GroupResponse> RegenerateInviteCodeAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>Deletes the group (owner only).</summary>
    Task DeleteAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-publishes every group's member list. Run at startup so services that keep a copy (Trips) catch up
    /// on groups created before they subscribed, or on events lost while they were down. Consumers ignore duplicates.
    /// </summary>
    Task RepublishMembersAsync(CancellationToken cancellationToken);
}
