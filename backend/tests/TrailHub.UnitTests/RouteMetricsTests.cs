using System.Text;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.BuildingBlocks.Infrastructure.Gpx;

namespace TrailHub.UnitTests;

public sealed class RouteMetricsTests
{
    [Fact]
    public void One_degree_of_latitude_is_about_111_km()
    {
        var distance = RouteMetrics.HaversineMeters(new GeoPoint(45, 10), new GeoPoint(46, 10));

        Assert.InRange(distance, 111_100, 111_300);
    }

    [Fact]
    public void Elevation_jitter_below_threshold_is_not_counted_as_climbing()
    {
        var flatButNoisy = Enumerable.Range(0, 100)
            .Select(i => new GeoPoint(45 + i * 0.0001, 10, 500 + (i % 2 == 0 ? 0 : 2)))
            .ToList();

        var metrics = RouteMetrics.Calculate(flatButNoisy);

        Assert.Equal(0, metrics.ElevationGainMeters);
        Assert.Equal(0, metrics.ElevationLossMeters);
    }

    [Fact]
    public void Gain_and_loss_follow_the_profile()
    {
        var route = new[] { 100.0, 150, 120, 300, 250 }.Select((e, i) => new GeoPoint(45 + i * 0.001, 10, e)).ToList();

        var metrics = RouteMetrics.Calculate(route);

        Assert.Equal(50 + 180, metrics.ElevationGainMeters);
        Assert.Equal(30 + 50, metrics.ElevationLossMeters);
        Assert.Equal(300, metrics.MaxElevationMeters);
        Assert.Equal(100, metrics.MinElevationMeters);
    }

    [Fact]
    public void Elevation_profile_is_downsampled_but_keeps_both_ends()
    {
        var route = Enumerable.Range(0, 1000).Select(i => new GeoPoint(45 + i * 0.0001, 10, i)).ToList();

        var profile = ElevationProfile.Build(route, maxSamples: 50);

        Assert.Equal(50, profile.Count);
        Assert.Equal(0, profile[0].ElevationMeters);
        Assert.Equal(999, profile[^1].ElevationMeters);
        Assert.Equal(0, profile[0].DistanceMeters);
    }

    [Fact]
    public void Elevation_profile_does_not_count_the_gap_between_paused_segments()
    {
        // Two 1-degree walks with a 10-degree jump between them while paused.
        IReadOnlyList<GeoPoint> first = [new(45, 10, 100), new(46, 10, 200)];
        IReadOnlyList<GeoPoint> second = [new(56, 10, 300), new(57, 10, 400)];

        var profile = ElevationProfile.Build([first, second]);

        Assert.Equal(4, profile.Count);
        Assert.Equal(profile[1].DistanceMeters, profile[2].DistanceMeters);
        Assert.InRange(profile[^1].DistanceMeters, 222_200, 222_600);
    }
}
