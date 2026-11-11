using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.UnitTests;

public sealed class RouteSharingTests
{
    private static PlannedRoute Route(bool snapped, double km)
    {
        var track = TrackTests.Line(km, [1000, 1100, 1200]);
        var waypoints = new List<RouteWaypoint> { new(46, 7, "Start", false), new(46.05, 7, "Finish", false) };
        return PlannedRoute.Create("owner", "Ridge", null, waypoints, new RoutedTrack(track, [0, 2], snapped), DateTime.UtcNow).Value;
    }

    [Fact]
    public void Only_routes_that_follow_paths_and_are_long_enough_can_be_published()
    {
        Assert.Equal(RouteErrors.PublishNeedsPaths, Route(snapped: false, km: 5).MarkPublished(DateTime.UtcNow).Error);
        Assert.Equal(RouteErrors.PublishTooShort, Route(snapped: true, km: 0.5).MarkPublished(DateTime.UtcNow).Error);

        var route = Route(snapped: true, km: 5);
        Assert.True(route.MarkPublished(DateTime.UtcNow).IsSuccess);
        Assert.NotNull(route.PublishedAtUtc);
    }

    [Fact]
    public void Sharing_with_a_group_is_idempotent_and_can_be_undone()
    {
        var route = Route(snapped: true, km: 5);
        var group = Guid.NewGuid();

        route.ShareWithGroup(group);
        route.ShareWithGroup(group);
        Assert.Equal([group], route.SharedWithGroups);

        route.UnshareGroup(group);
        Assert.Empty(route.SharedWithGroups);
    }

    [Fact]
    public void A_copy_belongs_to_the_new_owner_and_keeps_the_line()
    {
        var route = Route(snapped: true, km: 5);
        route.ShareWithGroup(Guid.NewGuid());

        var copy = route.CopyFor("member", DateTime.UtcNow);

        Assert.Equal("member", copy.OwnerId);
        Assert.NotEqual(route.Id, copy.Id);
        Assert.Equal(route.Points.Count, copy.Points.Count);
        Assert.Empty(copy.SharedWithGroups);
        Assert.Null(copy.PublishedAtUtc);
    }
}
