using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.UnitTests;

public sealed class TrackSimplifierTests
{
    [Fact]
    public void Short_tracks_are_returned_unchanged()
    {
        var points = Enumerable.Range(0, 10).Select(i => new GeoPoint(45 + i * 0.001, 10, null)).ToList();

        Assert.Same(points, TrackSimplifier.Simplify(points, 300));
    }

    [Fact]
    public void Long_tracks_keep_at_most_the_budget_including_both_ends()
    {
        var points = Enumerable.Range(0, 5_000)
            .Select(i => new GeoPoint(45 + i * 0.0001, 10 + Math.Sin(i / 50.0) * 0.01, null))
            .ToList();

        var simplified = TrackSimplifier.Simplify(points, 300);

        Assert.InRange(simplified.Count, 2, 300);
        Assert.Equal(points[0], simplified[0]);
        Assert.Equal(points[^1], simplified[^1]);
    }

    [Fact]
    public void Keeps_the_corner_of_a_track()
    {
        // An L shape: the corner is the point that matters most once the ends are kept.
        var up = Enumerable.Range(0, 500).Select(i => new GeoPoint(45 + i * 0.0001, 10, null));
        var right = Enumerable.Range(1, 500).Select(i => new GeoPoint(45.0499, 10 + i * 0.0001, null));
        var points = up.Concat(right).ToList();

        var simplified = TrackSimplifier.Simplify(points, 3);

        Assert.Equal([points[0], points[499], points[^1]], simplified);
    }

    [Fact]
    public void Collinear_points_are_dropped()
    {
        var points = Enumerable.Range(0, 1_000).Select(i => new GeoPoint(45 + i * 0.0001, 10, null)).ToList();

        var simplified = TrackSimplifier.Simplify(points, 300);

        Assert.Equal([points[0], points[^1]], simplified);
    }
}
