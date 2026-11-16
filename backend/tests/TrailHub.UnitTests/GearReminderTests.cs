using TrailHub.Trips.Domain.Trips;

namespace TrailHub.UnitTests;

public sealed class GearReminderTests
{
    private static readonly DateOnly Today = new(2027, 6, 1);

    private static Trip GroupTrip(DateOnly start)
    {
        var trip = Trip.Create("owner", "Tatra traverse", null, start, start.AddDays(2), DateTime.UtcNow).Value;
        trip.LinkGroup(Guid.NewGuid());
        return trip;
    }

    [Fact]
    public void Reminds_two_days_before_when_group_gear_is_unclaimed()
    {
        var trip = GroupTrip(Today.AddDays(2));
        trip.AddChecklistItem("owner", "Stove", ChecklistItemKind.Shared);
        trip.AddChecklistItem("owner", "Tent", ChecklistItemKind.Shared);
        trip.AddChecklistItem("owner", "Socks", ChecklistItemKind.Personal);

        Assert.True(trip.NeedsGearReminder(Today));
        Assert.Equal(2, trip.UnclaimedGroupItemCount);
        Assert.False(trip.NeedsGearReminder(Today.AddDays(-1)));
        Assert.False(trip.NeedsGearReminder(Today.AddDays(1)));
    }

    [Fact]
    public void Not_when_everything_is_claimed()
    {
        var trip = GroupTrip(Today.AddDays(2));
        var stove = trip.AddChecklistItem("owner", "Stove", ChecklistItemKind.Shared).Value;
        trip.ClaimChecklistItem("owner", stove.Id, claim: true);

        Assert.False(trip.NeedsGearReminder(Today));
    }

    [Fact]
    public void Only_once_per_trip()
    {
        var trip = GroupTrip(Today.AddDays(2));
        trip.AddChecklistItem("owner", "Stove", ChecklistItemKind.Shared);

        trip.MarkGearReminderSent(DateTime.UtcNow);

        Assert.False(trip.NeedsGearReminder(Today));
    }

    [Fact]
    public void Not_for_private_or_cancelled_trips()
    {
        var cancelled = GroupTrip(Today.AddDays(2));
        cancelled.AddChecklistItem("owner", "Stove", ChecklistItemKind.Shared);
        cancelled.Cancel();
        var solo = Trip.Create("owner", "Solo", null, Today.AddDays(2), Today.AddDays(3), DateTime.UtcNow).Value;

        Assert.False(cancelled.NeedsGearReminder(Today));
        Assert.False(solo.NeedsGearReminder(Today));
    }
}
