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

    [Fact]
    public void A_new_profile_never_uses_an_email_as_its_name()
    {
        var fromEmailName = UserProfile.Create("auth0|1", "kambleviciusrokas65@gmail.com", "kambleviciusrokas65@gmail.com", Now);
        var fromEmailOnly = UserProfile.Create("auth0|2", null, "anna.k_smith@x.io", Now);

        Assert.Equal("Kambleviciusrokas", fromEmailName.DisplayName);
        Assert.Equal("Anna K Smith", fromEmailOnly.DisplayName);
        Assert.False(fromEmailName.NameConfirmed);
    }

    [Fact]
    public void Existing_email_names_are_fixed_once()
    {
        var profile = UserProfile.Create("auth0|1", "Ada", "ada.l@x.io", Now);
        Assert.False(profile.FixEmailDisplayName());

        // A profile stored before names were made readable (only the database can hold one now).
        typeof(UserProfile).GetProperty(nameof(UserProfile.DisplayName))!.SetValue(profile, "ada.l@x.io");

        Assert.True(profile.FixEmailDisplayName());
        Assert.Equal("Ada L", profile.DisplayName);
        Assert.False(profile.FixEmailDisplayName());
        Assert.False(profile.NameConfirmed);
    }

    [Fact]
    public void Updating_details_confirms_the_name()
    {
        var profile = UserProfile.Create("auth0|1", "Ada", null, Now);

        Assert.Equal(ProfileErrors.InvalidDisplayName, profile.UpdateDetails("  ", null, null, null).Error);
        Assert.False(profile.NameConfirmed);

        Assert.True(profile.UpdateDetails("Ada", null, null, null).IsSuccess);
        Assert.True(profile.NameConfirmed);
        Assert.Equal("Ada", profile.DisplayName);
    }

    [Fact]
    public void An_email_typed_as_the_name_is_made_readable()
    {
        var profile = UserProfile.Create("auth0|1", "Ada", null, Now);

        profile.UpdateDetails("ada.lovelace@x.io", null, null, null);

        Assert.Equal("Ada Lovelace", profile.DisplayName);
    }
}
