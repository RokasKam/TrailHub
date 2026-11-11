namespace TrailHub.Profile.Application.Requests;

public sealed record UpdateProfileRequest(string DisplayName, string? Bio, string? HomeRegion, string? AvatarUrl);
