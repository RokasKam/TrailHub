using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Responses;

/// <param name="NameConfirmed">False until the person chooses or keeps their name; the app then asks once what to call them.</param>
public sealed record ProfileResponse(
    Guid Id,
    string UserId,
    string DisplayName,
    bool NameConfirmed,
    string? Email,
    string? Bio,
    string? HomeRegion,
    string? AvatarUrl,
    ProfilePreferences Preferences,
    HikingTotals Totals,
    int AchievementCount,
    DateTime CreatedAtUtc);
