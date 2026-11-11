namespace TrailHub.Profile.Application.Responses;

public sealed record AchievementResponse(string Code, string Title, string Description, bool Unlocked, DateTime? UnlockedAtUtc);
