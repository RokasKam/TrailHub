using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

public sealed class UserProfileTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void First_hike_unlocks_first_achievement_and_is_idempotent()
    {
        var profile = UserProfile.Create("auth0|1", "Ada", null, Now);
        var activityId = Guid.NewGuid();

        var unlocked = profile.RecordHike(activityId, 21_000, 1_200, Now, Now);
        var again = profile.RecordHike(activityId, 21_000, 1_200, Now, Now);

        Assert.Contains(unlocked, a => a.Code == "first-hike");
        Assert.Contains(unlocked, a => a.Code == "single-20km");
        Assert.Contains(unlocked, a => a.Code == "single-1000m");
        Assert.Empty(again);
        Assert.Equal(1, profile.Totals.CompletedHikes);
    }

    [Fact]
    public void Removing_a_hike_updates_totals_but_keeps_achievements()
    {
        var profile = UserProfile.Create("auth0|1", null, null, Now);
        var id = Guid.NewGuid();
        profile.RecordHike(id, 5_000, 100, Now, Now);

        profile.RemoveHike(id);

        Assert.Equal(0, profile.Totals.CompletedHikes);
        Assert.Single(profile.Achievements);
        Assert.Equal("Hiker", profile.DisplayName);
    }
}
