namespace TrailHub.BuildingBlocks.Domain.Geo;

public sealed record ElevationSample(double DistanceMeters, double ElevationMeters);

public static class ElevationProfile
{
    /// <summary>
    /// Builds a distance-vs-elevation series, downsampled to at most <paramref name="maxSamples"/>
    /// points so a 20k-point GPX track doesn't ship 20k samples to a phone chart.
    /// Points without elevation are skipped but still count towards distance.
    /// </summary>
    public static IReadOnlyList<ElevationSample> Build(IReadOnlyList<GeoPoint> points, int maxSamples = 200) =>
        Build([points], maxSamples);

    /// <summary>
    /// Same, for a track recorded in segments (pause/resume): the gap between segments isn't walked,
    /// so it doesn't add distance and the chart's x-axis matches the hike's distance.
    /// </summary>
    public static IReadOnlyList<ElevationSample> Build(IReadOnlyList<IReadOnlyList<GeoPoint>> segments, int maxSamples = 200)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSamples, 2);

        var samples = new List<ElevationSample>();
        double distance = 0;
        foreach (var points in segments)
        {
            for (var i = 0; i < points.Count; i++)
            {
                if (i > 0)
                {
                    distance += RouteMetrics.HaversineMeters(points[i - 1], points[i]);
                }

                if (points[i].Elevation is { } elevation)
                {
                    samples.Add(new ElevationSample(distance, elevation));
                }
            }
        }

        if (samples.Count <= maxSamples)
        {
            return samples;
        }

        var step = (samples.Count - 1) / (double)(maxSamples - 1);
        return Enumerable.Range(0, maxSamples)
            .Select(i => samples[(int)Math.Round(i * step)])
            .ToList();
    }
}
