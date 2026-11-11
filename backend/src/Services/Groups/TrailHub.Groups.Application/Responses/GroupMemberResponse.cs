using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Responses;

public sealed record GroupMemberResponse(string UserId, string DisplayName, GroupRole Role, DateTime JoinedAtUtc);
