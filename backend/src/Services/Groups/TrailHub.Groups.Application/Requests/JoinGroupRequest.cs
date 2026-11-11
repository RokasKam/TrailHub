namespace TrailHub.Groups.Application.Requests;

public sealed record JoinGroupRequest(string InviteCode, string? DisplayName);
