using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Responses;

public sealed record ProfileResponse(
    Guid Id,
    string UserId,
    string DisplayName,
    string? Email,
    string? Bio,
    string? HomeRegion,
    string? AvatarUrl,
    ProfilePreferences Preferences,
    HikingTotals Totals,
    int AchievementCount,
    DateTime CreatedAtUtc);
