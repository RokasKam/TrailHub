using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

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
