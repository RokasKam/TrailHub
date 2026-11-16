using TrailHub.Notifications.Application.Models;

namespace TrailHub.Notifications.Application.Requests;

/// <summary>A group membership snapshot from the Groups service.</summary>
public sealed record SyncGroupMembersRequest(Guid GroupId, string Name, IReadOnlyList<GroupMemberInfo> Members, bool Deleted, DateTime OccurredAtUtc);
