using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

public sealed class GearTests
{
    private static readonly DateOnly June1 = new(2027, 6, 1);

    [Fact]
    public void Gear_lists_drop_blank_and_duplicate_items()
    {
        var list = TrailHub.Trips.Domain.Gear.GearList.Create(
            "u",
            "Hut kit",
            [new("Liner", ChecklistItemKind.Personal), new(" liner ", ChecklistItemKind.Personal), new("  ", ChecklistItemKind.Personal), new("Stove", ChecklistItemKind.Shared)],
            DateTime.UtcNow).Value;

        Assert.Equal(["Liner", "Stove"], list.Items.Select(i => i.Text));
    }

    [Fact]
    public void Adding_a_list_skips_duplicates_and_makes_group_gear_personal_on_private_trips()
    {
        var trip = Trip.Create("u", "Solo", null, June1, June1.AddDays(2), DateTime.UtcNow).Value;
        trip.AddChecklistItem("u", "Headlamp");

        var added = trip.AddChecklistItems("u", [("headlamp", ChecklistItemKind.Personal), ("Tent", ChecklistItemKind.Shared), ("Map", ChecklistItemKind.Personal)]);

        Assert.Equal(2, added);
        Assert.Equal(3, trip.Checklist.Count);
        Assert.All(trip.Checklist, i => Assert.Equal(ChecklistItemKind.Personal, i.Kind));
    }

    [Fact]
    public void Suggestions_follow_nights_altitude_and_length_and_skip_what_you_have()
    {
        var facts = new TrailHub.Trips.Domain.Gear.TripGearFacts(Days: 4, TentNights: 1, HutNights: 2, HotelNights: 0, MaxElevationMeters: 2900, LongestDayMinutes: 420, Shared: true);

        var suggestions = TrailHub.Trips.Domain.Gear.GearSuggestions.For(facts, ["rain jacket"]);
        var texts = suggestions.Select(s => s.Text).ToList();

        Assert.Contains("Sleeping bag liner", texts);
        Assert.Contains("Tent", texts);
        Assert.Contains("Microspikes", texts);
        Assert.Contains("Water (2+ L)", texts);
        Assert.DoesNotContain("Rain jacket", texts);
        Assert.Equal(ChecklistItemKind.Shared, suggestions.Single(s => s.Text == "Tent").Kind);
        Assert.Equal("2 nights in huts", suggestions.Single(s => s.Text == "Earplugs").Reason);
    }

    [Fact]
    public void A_day_hike_gets_only_the_basics()
    {
        var facts = new TrailHub.Trips.Domain.Gear.TripGearFacts(1, 0, 0, 0, 1200, 180, Shared: false);

        var texts = TrailHub.Trips.Domain.Gear.GearSuggestions.For(facts, []).Select(s => s.Text).ToList();

        Assert.Equal(["First-aid kit", "Rain jacket", "Headlamp"], texts);
    }
}
