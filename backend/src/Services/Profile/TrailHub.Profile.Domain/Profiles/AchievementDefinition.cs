namespace TrailHub.Profile.Domain.Profiles;

public sealed record AchievementDefinition(string Code, string Title, string Description, Func<HikingTotals, bool> IsMet);
