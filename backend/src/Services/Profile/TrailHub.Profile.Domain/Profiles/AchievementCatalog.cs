namespace TrailHub.Profile.Domain.Profiles;

public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All =
    [
        new("first-hike", "First Steps", "Complete your first hike.", t => t.CompletedHikes >= 1),
        new("hikes-10", "Regular", "Complete 10 hikes.", t => t.CompletedHikes >= 10),
        new("hikes-50", "Trail Veteran", "Complete 50 hikes.", t => t.CompletedHikes >= 50),
        new("distance-50km", "Fifty Down", "Hike 50 km in total.", t => t.DistanceMeters >= 50_000),
        new("distance-100km", "Century", "Hike 100 km in total.", t => t.DistanceMeters >= 100_000),
        new("distance-500km", "Long Walker", "Hike 500 km in total.", t => t.DistanceMeters >= 500_000),
        new("distance-1000km", "Thousand Kilometers", "Hike 1,000 km in total.", t => t.DistanceMeters >= 1_000_000),
        new("climb-1000m", "Climber", "Gain 1,000 m of elevation in total.", t => t.ElevationGainMeters >= 1_000),
        new("climb-everest", "Everest Equivalent", "Gain 8,849 m of elevation in total.", t => t.ElevationGainMeters >= 8_849),
        new("climb-25000m", "Sky Walker", "Gain 25,000 m of elevation in total.", t => t.ElevationGainMeters >= 25_000),
        new("single-20km", "Long Day", "Hike 20 km in a single activity.", t => t.LongestHikeMeters >= 20_000),
        new("single-1000m", "Big Climb", "Gain 1,000 m in a single activity.", t => t.BiggestClimbMeters >= 1_000),
    ];
}
