using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.UnitTests;

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
