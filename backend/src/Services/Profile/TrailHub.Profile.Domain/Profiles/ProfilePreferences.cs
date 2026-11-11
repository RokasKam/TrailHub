namespace TrailHub.Profile.Domain.Profiles;

public sealed record ProfilePreferences(
    UnitSystem Units = UnitSystem.Metric,
    MapStyle MapStyle = MapStyle.Outdoors,
    bool ShareLocationWithGroups = true,
    bool NotificationsEnabled = true);
