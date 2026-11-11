using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.UnitTests;

public sealed class PlannedRouteTests
{
    [Fact]
    public void Overnight_waypoints_split_the_route_into_days_but_not_at_start_or_finish()
    {
        var track = TrackTests.Line(30, [1000, 1100, 1200, 1100, 1000, 900, 1000]);
        var waypoints = new List<RouteWaypoint>
        {
            new(46, 7, "Start", IsOvernight: true),
            new(46.1, 7, "Hut", IsOvernight: true),
            new(46.27, 7, "Finish", IsOvernight: true),
        };

        var route = PlannedRoute.Create("u", "Traverse", null, waypoints, new RoutedTrack(track, [0, 3, 6], Snapped: true), DateTime.UtcNow).Value;

        Assert.Equal(2, route.DayCount);
        Assert.Equal([(0, 3), (3, 6)], route.Days().Select(d => (d.FromIndex, d.ToIndex)));
        Assert.Equal("Hut", route.Days()[0].Overnight?.Name);
    }

    [Fact]
    public void Needs_two_to_fifty_waypoints()
    {
        var track = TrackTests.Line(1, [1000, 1000]);
        var one = new List<RouteWaypoint> { new(46, 7, null, false) };

        Assert.Equal(RouteErrors.WaypointCount, PlannedRoute.Create("u", "R", null, one, new RoutedTrack(track, [0], true), DateTime.UtcNow).Error);
    }
}
