using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

public sealed class TrailTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_computes_metrics_and_naismith_duration()
    {
        // ~10 km due north climbing 600 m: Naismith = 2h flat + 1h climbing.
        var route = Enumerable.Range(0, 101).Select(i => new GeoPoint(45 + i * 0.000899, 10, 1000 + i * 6)).ToList();

        var trail = Trail.Create("Test", null, "Alps", Difficulty.Moderate, route, "user-1", Now).Value;

        Assert.InRange(trail.DistanceMeters, 9_950, 10_050);
        Assert.Equal(600, trail.ElevationGainMeters);
        Assert.InRange(trail.EstimatedDurationMinutes, 179, 181);
        Assert.False(trail.IsLoop);
    }

    [Fact]
    public void Route_needs_two_points()
    {
        var result = Trail.Create("Test", null, "Alps", Difficulty.Easy, [new GeoPoint(45, 10)], "u", Now);

        Assert.Equal(TrailErrors.RouteTooShort, result.Error);
    }

    [Fact]
    public void Start_close_to_end_is_a_loop()
    {
        var route = new List<GeoPoint> { new(45, 10), new(45.01, 10), new(45.01, 10.01), new(45.0005, 10) };

        Assert.True(Trail.Create("Loop", null, "Alps", Difficulty.Easy, route, "u", Now).Value.IsLoop);
    }

    [Fact]
    public void Only_creator_can_edit_details()
    {
        var trail = Trail.Create("Test", null, "Alps", Difficulty.Easy, [new(45, 10), new(45.1, 10)], "owner", Now).Value;

        Assert.Equal(TrailErrors.NotOwner, trail.UpdateDetails("New", null, "Alps", Difficulty.Hard, "someone-else").Error);
    }

    [Fact]
    public void Uploaded_photos_must_be_stored_under_the_trails_own_prefix()
    {
        var trail = Trail.Create("Test", null, "Alps", Difficulty.Easy, [new(45, 10), new(45.1, 10)], "u", Now).Value;
        var other = Guid.NewGuid();

        Assert.True(trail.AddUploadedPhoto($"{Trail.PhotoKeyPrefix(trail.Id)}a.jpg", "Summit", "u", Now).IsSuccess);
        Assert.Equal(TrailErrors.InvalidPhotoUrl, trail.AddUploadedPhoto($"{Trail.PhotoKeyPrefix(other)}a.jpg", null, "u", Now).Error);
        Assert.Equal(TrailErrors.InvalidPhotoUrl, trail.AddUploadedPhoto($"{Trail.PhotoKeyPrefix(trail.Id)}../x.jpg", null, "u", Now).Error);
        Assert.Single(trail.Photos);
    }

    [Theory]
    [InlineData("Drinking Water", PointOfInterestType.WaterSource)]
    [InlineData("Refuge Bonatti", PointOfInterestType.Shelter)]
    [InlineData("Campground", PointOfInterestType.Campsite)]
    [InlineData("Summit", PointOfInterestType.Viewpoint)]
    [InlineData("Parking", PointOfInterestType.Other)]
    public void Waypoint_names_map_to_poi_types(string text, PointOfInterestType expected) =>
        Assert.Equal(expected, PointOfInterest.InferType(text));
}

public sealed class HikeActivityTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Pauses_count_towards_elapsed_but_not_moving_time_or_distance()
    {
        var first = Segment(0, 0, minutes: 60);         // 08:00–09:00
        var second = Segment(1, 0.05, minutes: 30, startOffsetMinutes: 120); // 10:00–10:30, starts ~5.5 km away

        var activity = HikeActivity.Create(Guid.NewGuid(), "u", "Hike", null, [first, second], ActivitySource.Recorded, null, Start.AddDays(1)).Value;

        Assert.Equal(90 * 60, activity.MovingDurationSeconds);
        Assert.Equal(150 * 60, activity.ElapsedDurationSeconds);
        Assert.InRange(activity.DistanceMeters, 1_000, 1_300); // two ~555 m segments, not the 5.5 km jump
    }

    [Fact]
    public void Timestamps_must_not_go_backwards()
    {
        var segment = new ActivitySegment([new(45, 10, null, Start.AddMinutes(5)), new(45.001, 10, null, Start)]);

        var result = HikeActivity.Create(Guid.NewGuid(), "u", "Hike", null, [segment], ActivitySource.Recorded, null, Start.AddDays(1));

        Assert.Equal(ActivityErrors.TimestampsOutOfOrder, result.Error);
    }

    [Fact]
    public void Requires_client_generated_id()
    {
        var result = HikeActivity.Create(Guid.Empty, "u", "Hike", null, [Segment(0, 0, 10)], ActivitySource.Recorded, null, Start.AddDays(1));

        Assert.Equal(ActivityErrors.MissingId, result.Error);
    }

    private static ActivitySegment Segment(int index, double latOffset, int minutes, int startOffsetMinutes = 0) =>
        new(Enumerable.Range(0, minutes + 1)
            .Select(m => new TrackPoint(45 + latOffset + m * 0.005 / minutes, 10, 500, Start.AddMinutes(startOffsetMinutes + m)))
            .ToList());
}

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

public sealed class HikingGroupTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static HikingGroup NewGroup() => HikingGroup.Create("Weekenders", null, "owner", "Owner", "ABCD2345", Now).Value;

    [Fact]
    public void Join_requires_the_current_invite_code()
    {
        var group = NewGroup();

        Assert.Equal(GroupErrors.InvalidInviteCode, group.Join("u2", "Bo", "WRONG123", Now).Error);
        Assert.True(group.Join("u2", "Bo", "abcd2345", Now).IsSuccess);
        Assert.Equal(GroupErrors.AlreadyMember, group.Join("u2", "Bo", "ABCD2345", Now).Error);
    }

    [Fact]
    public void Owner_must_transfer_before_leaving()
    {
        var group = NewGroup();
        group.Join("u2", "Bo", "ABCD2345", Now);

        Assert.Equal(GroupErrors.OwnerCannotLeave, group.Leave("owner").Error);
        group.TransferOwnership("owner", "u2");
        Assert.True(group.Leave("owner").IsSuccess);
        Assert.Equal("u2", group.OwnerId);
    }

    [Fact]
    public void Members_rsvp_once_per_hike_and_the_organizer_is_going()
    {
        var group = NewGroup();
        group.Join("u2", "Bo", "ABCD2345", Now);
        var hike = group.ScheduleHike("owner", "Sunrise", null, Now.AddDays(2), null, null, Now).Value;

        group.Rsvp("u2", hike.Id, RsvpStatus.Maybe, Now);
        group.Rsvp("u2", hike.Id, RsvpStatus.Going, Now);

        Assert.Equal(2, hike.Rsvps.Count(r => r.Status == RsvpStatus.Going));
        Assert.Equal(GroupErrors.NotMember, group.Rsvp("stranger", hike.Id, RsvpStatus.Going, Now).Error);
        Assert.Equal(GroupErrors.HikeClosed, group.Rsvp("u2", hike.Id, RsvpStatus.NotGoing, Now.AddDays(3)).Error);

        group.Leave("u2");
        Assert.Single(hike.Rsvps);
    }

    [Fact]
    public void Hikes_must_be_in_the_future_and_scheduled_by_members()
    {
        var group = NewGroup();

        Assert.Equal(GroupErrors.HikeInPast, group.ScheduleHike("owner", "Sunrise", null, Now.AddHours(-1), null, null, Now).Error);
        Assert.Equal(GroupErrors.NotMember, group.ScheduleHike("stranger", "Sunrise", null, Now.AddDays(1), null, null, Now).Error);
        Assert.True(group.ScheduleHike("owner", "Sunrise", null, Now.AddDays(1), null, null, Now).IsSuccess);
    }
}

public sealed class HikingConditionsTests
{
    [Fact]
    public void Thunderstorms_are_poor_hiking_weather() =>
        Assert.Equal(HikingSuitability.Poor, HikingConditions.Assess(Day(code: 95)).Suitability);

    [Fact]
    public void Strong_gusts_need_caution() =>
        Assert.Equal(HikingSuitability.Caution, HikingConditions.Assess(Day(gusts: 50)).Suitability);

    [Fact]
    public void Calm_clear_day_is_good()
    {
        var assessment = HikingConditions.Assess(Day());

        Assert.Equal(HikingSuitability.Good, assessment.Suitability);
        Assert.Empty(assessment.Warnings);
    }

    private static DailyForecast Day(int code = 0, double gusts = 10) =>
        new(new DateOnly(2026, 10, 2), code, 5, 18, 0, 0, 8, gusts, 3, DateTime.Today.AddHours(7), DateTime.Today.AddHours(18));
}

public sealed class TrailModerationTests
{
    private static Trail NewTrail() =>
        Trail.Create("Ridge", null, "Alps", Difficulty.Moderate, [new GeoPoint(46, 7, 1000), new GeoPoint(46.01, 7, 1100)], "author", DateTime.UtcNow).Value;

    [Fact]
    public void Authors_cant_undo_a_moderators_decision()
    {
        var trail = NewTrail();
        trail.Hide(HiddenBy.Moderator, DateTime.UtcNow);

        Assert.Equal(TrailErrors.HiddenByModerators, trail.Unhide(HiddenBy.Author).Error);
        Assert.True(trail.IsHidden);
        Assert.True(trail.Unhide(HiddenBy.Moderator).IsSuccess);
        Assert.False(trail.IsHidden);
    }

    [Fact]
    public void Reports_dont_override_a_moderators_hide_and_authors_can_undo_their_own_unpublish()
    {
        var trail = NewTrail();
        trail.Hide(HiddenBy.Moderator, DateTime.UtcNow);
        trail.Hide(HiddenBy.Reports, DateTime.UtcNow);
        Assert.Equal(HiddenBy.Moderator, trail.HiddenBy);

        var other = NewTrail();
        other.Hide(HiddenBy.Author, DateTime.UtcNow);
        Assert.True(other.Unhide(HiddenBy.Author).IsSuccess);
    }

    [Fact]
    public void Authors_cant_report_their_own_trail()
    {
        var trail = NewTrail();
        Assert.Equal(TrailErrors.CantReportOwnTrail, TrailHub.Trails.Domain.Community.TrailReport.Create(trail, "author", TrailHub.Trails.Domain.Community.ReportReason.Spam, null, DateTime.UtcNow).Error);
        Assert.True(TrailHub.Trails.Domain.Community.TrailReport.Create(trail, "someone", TrailHub.Trails.Domain.Community.ReportReason.Dangerous, "Closed after a rockfall", DateTime.UtcNow).IsSuccess);
    }
}
