using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

public sealed class TripTests
{
    private static readonly DateOnly June1 = new(2027, 6, 1);

    [Fact]
    public void Creates_one_day_per_date_and_reschedule_keeps_existing_plans()
    {
        var trip = Trip.Create("u", "Tour du Mont Blanc", null, June1, June1.AddDays(4), DateTime.UtcNow).Value;
        var trail = Guid.NewGuid();
        trip.AddTrail(2, trail);

        trip.Reschedule(June1, June1.AddDays(2));

        Assert.Equal(3, trip.Days.Count);
        Assert.Contains(trail, trip.Days.Single(d => d.DayNumber == 2).TrailIds);
    }

    [Fact]
    public void Same_trail_twice_on_a_day_is_a_conflict()
    {
        var trip = Trip.Create("u", "Trip", null, June1, June1, DateTime.UtcNow).Value;
        var trail = Guid.NewGuid();
        trip.AddTrail(1, trail);

        Assert.Equal(TripErrors.TrailAlreadyOnDay, trip.AddTrail(1, trail).Error);
    }

    [Fact]
    public void Cancelled_trips_are_read_only()
    {
        var trip = Trip.Create("u", "Trip", null, June1, June1, DateTime.UtcNow).Value;
        trip.Cancel();

        Assert.Equal(TripErrors.NotEditable, trip.AddTrail(1, Guid.NewGuid()).Error);
    }

    [Fact]
    public void Rejects_end_before_start_and_overlong_trips()
    {
        Assert.Equal(TripErrors.EndBeforeStart, Trip.Create("u", "T", null, June1, June1.AddDays(-1), DateTime.UtcNow).Error);
        Assert.Equal(TripErrors.TooLong, Trip.Create("u", "T", null, June1, June1.AddDays(Trip.MaxDays), DateTime.UtcNow).Error);
    }

    private static Trip SharedTrip()
    {
        var trip = Trip.Create("owner", "Tatra traverse", null, June1, June1.AddDays(2), DateTime.UtcNow).Value;
        trip.LinkGroup(Guid.NewGuid());
        return trip;
    }

    [Fact]
    public void Personal_items_are_ticked_per_person()
    {
        var trip = SharedTrip();
        var headlamp = trip.AddChecklistItem("owner", "Headlamp").Value;

        trip.SetChecklistItemDone("anna", headlamp.Id, true);

        Assert.True(headlamp.IsDoneFor("anna"));
        Assert.False(headlamp.IsDoneFor("owner"));
    }

    [Fact]
    public void Shared_gear_has_one_claimer_and_only_they_can_tick_it()
    {
        var trip = SharedTrip();
        var stove = trip.AddChecklistItem("owner", "Stove", ChecklistItemKind.Shared).Value;

        Assert.True(trip.ClaimChecklistItem("anna", stove.Id, true).IsSuccess);

        Assert.Equal(TripErrors.ClaimedBySomeoneElse, trip.ClaimChecklistItem("tom", stove.Id, true).Error);
        Assert.Equal(TripErrors.ClaimedBySomeoneElse, trip.SetChecklistItemDone("tom", stove.Id, true).Error);
        Assert.True(trip.SetChecklistItemDone("anna", stove.Id, true).IsSuccess);
        Assert.True(stove.IsDone);
    }

    [Fact]
    public void Ticking_unclaimed_gear_claims_it_and_the_owner_can_release_any_claim()
    {
        var trip = SharedTrip();
        var tent = trip.AddChecklistItem("owner", "Tent", ChecklistItemKind.Shared).Value;

        trip.SetChecklistItemDone("tom", tent.Id, true);
        Assert.Equal("tom", tent.ClaimedBy);

        Assert.True(trip.ClaimChecklistItem("owner", tent.Id, false).IsSuccess);
        Assert.Null(tent.ClaimedBy);
        Assert.False(tent.IsDone);
    }

    [Fact]
    public void Shared_gear_needs_a_group_and_members_remove_only_their_own_items()
    {
        var solo = Trip.Create("owner", "Solo", null, June1, June1, DateTime.UtcNow).Value;
        Assert.Equal(TripErrors.SharedItemNeedsGroup, solo.AddChecklistItem("owner", "Stove", ChecklistItemKind.Shared).Error);

        var trip = SharedTrip();
        var map = trip.AddChecklistItem("owner", "Map", ChecklistItemKind.Shared).Value;
        var snacks = trip.AddChecklistItem("anna", "Snacks").Value;

        Assert.Equal(TripErrors.NotYourItem, trip.RemoveChecklistItem("anna", map.Id).Error);
        Assert.True(trip.RemoveChecklistItem("anna", snacks.Id).IsSuccess);
        Assert.True(trip.RemoveChecklistItem("owner", map.Id).IsSuccess);
    }

    [Fact]
    public void People_who_leave_the_group_lose_claims_and_ticks()
    {
        var trip = SharedTrip();
        var stove = trip.AddChecklistItem("owner", "Stove", ChecklistItemKind.Shared).Value;
        var water = trip.AddChecklistItem("owner", "Water").Value;
        trip.ClaimChecklistItem("anna", stove.Id, true);
        trip.SetChecklistItemDone("anna", water.Id, true);
        trip.SetChecklistItemDone("owner", water.Id, true);

        Assert.True(trip.SyncGroupMembers(["owner", "tom"]));

        Assert.Null(stove.ClaimedBy);
        Assert.Equal(["owner"], water.DoneBy);
        Assert.False(trip.SyncGroupMembers(["owner", "tom"]));
    }

    [Fact]
    public void Trip_becomes_private_when_its_owner_leaves_the_group()
    {
        var trip = SharedTrip();
        trip.AddChecklistItem("owner", "Stove", ChecklistItemKind.Shared);

        trip.SyncGroupMembers(["anna"]);

        Assert.Null(trip.GroupId);
    }
}
