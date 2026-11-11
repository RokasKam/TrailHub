using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.UnitTests;

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
