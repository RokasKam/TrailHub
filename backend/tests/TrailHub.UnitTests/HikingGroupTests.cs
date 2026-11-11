using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

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
