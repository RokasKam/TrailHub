namespace TrailHub.Groups.Application.Responses;

/// <param name="InviteCode">Only returned to the owner.</param>
public sealed record GroupResponse(Guid Id, string Name, string? Description, string? InviteCode, IReadOnlyList<GroupMemberResponse> Members, IReadOnlyList<GroupHikeResponse> UpcomingHikes, DateTime CreatedAtUtc);
