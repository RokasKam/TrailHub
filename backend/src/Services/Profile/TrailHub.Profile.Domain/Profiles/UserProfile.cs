using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Profile.Domain.Profiles;

public enum UnitSystem
{
    Metric,
    Imperial,
}

public enum MapStyle
{
    Outdoors,
    Satellite,
    Terrain,
}

public sealed record ProfilePreferences(
    UnitSystem Units = UnitSystem.Metric,
    MapStyle MapStyle = MapStyle.Outdoors,
    bool ShareLocationWithGroups = true,
    bool NotificationsEnabled = true);

public sealed record HikingTotals(int CompletedHikes, double DistanceMeters, double ElevationGainMeters, double LongestHikeMeters, double BiggestClimbMeters);

/// <summary>A hike counted towards this profile's totals. Also makes event handling idempotent.</summary>
public sealed record RecordedHike(Guid ActivityId, double DistanceMeters, double ElevationGainMeters, DateTime CompletedAtUtc);

public sealed record UnlockedAchievement(string Code, DateTime UnlockedAtUtc);

/// <summary>
/// The app-side profile for an Auth0 user. Identity (credentials, social logins, MFA) lives in
/// Auth0; this aggregate owns everything TrailHub-specific about the person.
/// </summary>
public sealed class UserProfile : AggregateRoot<Guid>
{
    public const int MaxDisplayNameLength = 80;
    public const int MaxBioLength = 1000;

    private readonly List<RecordedHike> _hikes = [];
    private readonly List<UnlockedAchievement> _achievements = [];

    private UserProfile()
    {
    }

    /// <summary>Auth0 subject (<c>sub</c>), e.g. <c>auth0|65f…</c> or <c>google-oauth2|…</c>.</summary>
    public string UserId { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string? Email { get; private set; }

    public string? Bio { get; private set; }

    public string? HomeRegion { get; private set; }

    public string? AvatarUrl { get; private set; }

    public ProfilePreferences Preferences { get; private set; } = new();

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<RecordedHike> Hikes => _hikes;

    public IReadOnlyList<UnlockedAchievement> Achievements => _achievements;

    public HikingTotals Totals => new(
        _hikes.Count,
        _hikes.Sum(h => h.DistanceMeters),
        _hikes.Sum(h => h.ElevationGainMeters),
        _hikes.Count == 0 ? 0 : _hikes.Max(h => h.DistanceMeters),
        _hikes.Count == 0 ? 0 : _hikes.Max(h => h.ElevationGainMeters));

    public static UserProfile Create(string userId, string? displayName, string? email, DateTime nowUtc) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DisplayName = NormalizeDisplayName(displayName),
            Email = email,
            CreatedAtUtc = nowUtc,
        };

    public Result UpdateDetails(string displayName, string? bio, string? homeRegion, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > MaxDisplayNameLength)
        {
            return ProfileErrors.InvalidDisplayName;
        }

        if (bio?.Length > MaxBioLength)
        {
            return ProfileErrors.BioTooLong;
        }

        if (avatarUrl is not null && (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            return ProfileErrors.InvalidAvatarUrl;
        }

        DisplayName = displayName.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        HomeRegion = string.IsNullOrWhiteSpace(homeRegion) ? null : homeRegion.Trim();
        AvatarUrl = avatarUrl;
        return Result.Success();
    }

    public void UpdatePreferences(ProfilePreferences preferences) => Preferences = preferences;

    /// <summary>Counts a completed hike and returns any achievements it unlocked. Recording the same activity twice is a no-op.</summary>
    public IReadOnlyList<AchievementDefinition> RecordHike(Guid activityId, double distanceMeters, double elevationGainMeters, DateTime completedAtUtc, DateTime nowUtc)
    {
        if (_hikes.Any(h => h.ActivityId == activityId))
        {
            return [];
        }

        _hikes.Add(new RecordedHike(activityId, distanceMeters, elevationGainMeters, completedAtUtc));

        var totals = Totals;
        var unlocked = AchievementCatalog.All
            .Where(a => _achievements.All(u => u.Code != a.Code) && a.IsMet(totals))
            .ToList();
        _achievements.AddRange(unlocked.Select(a => new UnlockedAchievement(a.Code, nowUtc)));
        return unlocked;
    }

    /// <summary>Removes a deleted hike from the totals. Achievements already earned are kept.</summary>
    public void RemoveHike(Guid activityId) => _hikes.RemoveAll(h => h.ActivityId == activityId);

    private static string NormalizeDisplayName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "Hiker" : trimmed[..Math.Min(trimmed.Length, MaxDisplayNameLength)];
    }
}

public static class ProfileErrors
{
    public static readonly Error InvalidDisplayName = Error.Validation("Profile.InvalidDisplayName", $"Display name is required and must be at most {UserProfile.MaxDisplayNameLength} characters.");
    public static readonly Error BioTooLong = Error.Validation("Profile.BioTooLong", $"Bio must be at most {UserProfile.MaxBioLength} characters.");
    public static readonly Error InvalidAvatarUrl = Error.Validation("Profile.InvalidAvatarUrl", "Avatar URL must be an absolute https URL.");
}
