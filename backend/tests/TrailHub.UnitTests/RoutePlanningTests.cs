using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.UnitTests;

public sealed class HikingTimeTests
{
    [Fact]
    public void Flat_walking_is_four_km_per_hour()
    {
        Assert.Equal(180, HikingTime.EstimateMinutes(12_000, 0, 0));
    }

    [Fact]
    public void Adds_half_of_the_smaller_of_horizontal_and_vertical_time()
    {
        // 8 km = 2 h horizontal; 900 m up = 3 h vertical → 3 h + 1 h.
        Assert.Equal(240, HikingTime.EstimateMinutes(8_000, 900, 0));
    }

    [Fact]
    public void Rough_trails_take_longer()
    {
        Assert.True(HikingTime.EstimateMinutes(8_000, 900, 0, HikingTime.TerrainFactor(4)) > HikingTime.EstimateMinutes(8_000, 900, 0));
    }
}

public sealed class TrackTests
{
    [Fact]
    public void Measures_any_stretch_with_climbing_and_time()
    {
        var track = Line(km: 10, elevations: [1000, 1100, 1200, 1100, 1000]);

        var firstHalf = track.Measure(0, 2);

        Assert.InRange(firstHalf.DistanceMeters, 4_900, 5_100);
        Assert.Equal(200, firstHalf.AscentMeters, 1);
        Assert.Equal(0, firstHalf.DescentMeters, 1);
        Assert.Equal(200, track.Total.DescentMeters, 1);
        Assert.Equal(track.Total.DurationMinutes, track.Minutes(0, track.LastIndex));
    }

    /// <summary>A line heading north from 46°N with evenly spaced points.</summary>
    internal static Track Line(double km, double[] elevations)
    {
        var step = km / (elevations.Length - 1) / 111.2; // degrees of latitude per step
        return new Track(elevations.Select((e, i) => new GeoPoint(46 + (i * step), 7, e)).ToList());
    }
}

public sealed class ItineraryPlannerTests
{
    // 60 km of gentle terrain, a point every 250 m.
    private static readonly Track Route = TrackTests.Line(60, Enumerable.Range(0, 241).Select(i => 1500 + (100 * Math.Sin(i / 10.0))).ToArray());

    private static OvernightCandidate At(double km, string name, PoiKind kind, double offTrack = 100)
    {
        var i = Route.IndexAtDistance(km * 1000);
        return new OvernightCandidate(name, kind, Route.Points[i], i, offTrack);
    }

    [Fact]
    public void Splits_into_the_requested_days_ending_at_preferred_accommodation()
    {
        var candidates = new[] { At(19, "Hut A", PoiKind.MountainHut), At(20, "Camp A", PoiKind.Campsite), At(41, "Hut B", PoiKind.MountainHut), At(40, "Camp B", PoiKind.Campsite) };

        var plan = ItineraryPlanner.Plan(Route, candidates, new ItineraryOptions(3, null, Accommodation.Hut, PlanDifficulty.Moderate));

        Assert.Equal(3, plan.Stages.Count);
        Assert.Equal(["Hut A", "Hut B"], plan.Stages.Take(2).Select(s => s.Overnight!.Name));
        Assert.Null(plan.Stages[^1].Overnight);
        Assert.Equal(Route.LastIndex, plan.Stages[^1].ToIndex);
    }

    [Fact]
    public void Prefers_campsites_for_tent_campers()
    {
        var candidates = new[] { At(19, "Hut A", PoiKind.MountainHut), At(20, "Camp A", PoiKind.Campsite), At(41, "Hut B", PoiKind.MountainHut), At(40, "Camp B", PoiKind.Campsite) };

        var plan = ItineraryPlanner.Plan(Route, candidates, new ItineraryOptions(3, null, Accommodation.Tent, PlanDifficulty.Moderate));

        Assert.Equal(["Camp A", "Camp B"], plan.Stages.Take(2).Select(s => s.Overnight!.Name));
    }

    [Fact]
    public void Respects_the_daily_maximum_by_adding_days_and_says_so()
    {
        var plan = ItineraryPlanner.Plan(Route, [], new ItineraryOptions(2, 18_000, Accommodation.Tent, PlanDifficulty.Hard));

        Assert.True(plan.Stages.Count >= 4);
        Assert.All(plan.Stages, s => Assert.True(Route.DistanceAt(s.ToIndex) - Route.DistanceAt(s.FromIndex) <= 18_000));
        Assert.Contains(plan.Warnings, w => w.Contains("needs about", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_accommodation_days_end_at_plain_points_with_a_warning()
    {
        var plan = ItineraryPlanner.Plan(Route, [], new ItineraryOptions(3, null, Accommodation.Hotel, PlanDifficulty.Moderate));

        Assert.Equal(3, plan.Stages.Count);
        Assert.Equal(ItineraryPlanner.FallbackName, plan.Stages[0].Overnight!.Name);
        Assert.Contains(plan.Warnings, w => w.Contains("no accommodation", StringComparison.Ordinal));
    }

    [Fact]
    public void Plans_fewer_days_when_the_route_is_too_short()
    {
        var shortRoute = TrackTests.Line(3, [1000, 1000, 1000, 1000]);

        var plan = ItineraryPlanner.Plan(shortRoute, [], new ItineraryOptions(5, null, Accommodation.Tent, PlanDifficulty.Easy));

        Assert.True(plan.Stages.Count < 5);
        Assert.Contains(plan.Warnings, w => w.Contains("too short", StringComparison.Ordinal));
    }
}

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
