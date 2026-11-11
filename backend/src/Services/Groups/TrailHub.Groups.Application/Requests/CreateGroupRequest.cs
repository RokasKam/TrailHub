namespace TrailHub.Groups.Application.Requests;

public sealed record CreateGroupRequest(string Name, string? Description, string? DisplayName);
