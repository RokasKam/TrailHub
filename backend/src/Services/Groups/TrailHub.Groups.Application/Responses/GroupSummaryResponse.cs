using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Responses;

public sealed record GroupSummaryResponse(Guid Id, string Name, int MemberCount, GroupRole MyRole, DateTime? NextHikeAtUtc);
