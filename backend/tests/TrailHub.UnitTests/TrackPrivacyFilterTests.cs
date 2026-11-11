using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.UnitTests;

public sealed class TrackPrivacyFilterTests
{
    [Fact]
    public void Full_returns_the_whole_track()
    {
        var segments = new[] { Line(45, 40) };

        var visible = TrackPrivacyFilter.Apply(segments, TrackPrivacy.Full);

        Assert.Equal(40, visible.Single().Count);
    }

    [Fact]
    public void Stats_only_returns_no_track()
    {
        Assert.Empty(TrackPrivacyFilter.Apply([Line(45, 40)], TrackPrivacy.StatsOnly));
    }

    [Fact]
    public void Hide_ends_drops_points_within_200_m_of_the_start_and_finish()
    {
        var track = Line(45, 40); // ~55 m apart, ~2.2 km in all
        var start = track[0];
        var finish = track[^1];

        var visible = TrackPrivacyFilter.Apply([track], TrackPrivacy.HideEnds);

        var points = Assert.Single(visible);
        Assert.InRange(points.Count, 30, 34);
        Assert.All(points, p =>
        {
            Assert.True(RouteMetrics.HaversineMeters(p, start) >= TrackPrivacyFilter.HiddenEndRadiusMeters);
            Assert.True(RouteMetrics.HaversineMeters(p, finish) >= TrackPrivacyFilter.HiddenEndRadiusMeters);
        });
    }

    [Fact]
    public void Hide_ends_splits_a_loop_where_it_passes_home_again()
    {
        // Out 2.2 km, back home, out again: the middle pass by home is hidden and the line is split there.
        var outward = Line(45, 40);
        var loop = outward.Concat(outward.AsEnumerable().Reverse()).Concat(outward).ToList();

        var visible = TrackPrivacyFilter.Apply([loop], TrackPrivacy.HideEnds);

        Assert.True(visible.Count >= 2);
        Assert.All(visible.SelectMany(p => p), p => Assert.True(RouteMetrics.HaversineMeters(p, loop[0]) >= TrackPrivacyFilter.HiddenEndRadiusMeters));
    }

    [Fact]
    public void Hide_ends_on_a_short_track_hides_everything()
    {
        Assert.Empty(TrackPrivacyFilter.Apply([Line(45, 6)], TrackPrivacy.HideEnds)); // ~275 m long
    }

    private static List<GeoPoint> Line(double startLatitude, int count) =>
        Enumerable.Range(0, count).Select(i => new GeoPoint(startLatitude + i * 0.0005, 10, null)).ToList();
}
