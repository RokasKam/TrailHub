namespace TrailHub.BuildingBlocks.Domain.Geo;

public sealed record GeoBounds(double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude)
{
    public static GeoBounds From(IEnumerable<GeoPoint> points)
    {
        double minLat = double.MaxValue, minLon = double.MaxValue, maxLat = double.MinValue, maxLon = double.MinValue;
        var any = false;
        foreach (var p in points)
        {
            any = true;
            minLat = Math.Min(minLat, p.Latitude);
            minLon = Math.Min(minLon, p.Longitude);
            maxLat = Math.Max(maxLat, p.Latitude);
            maxLon = Math.Max(maxLon, p.Longitude);
        }

        return any ? new GeoBounds(minLat, minLon, maxLat, maxLon) : throw new ArgumentException("At least one point is required.", nameof(points));
    }
}

public static class RouteSampling
{
    /// <summary>Keeps at most <paramref name="maxPoints"/> evenly spaced points, always including both ends.</summary>
    public static IReadOnlyList<T> Downsample<T>(IReadOnlyList<T> points, int? maxPoints)
    {
        if (maxPoints is not { } max || points.Count <= max || max < 2)
        {
            return points;
        }

        var step = (points.Count - 1) / (double)(max - 1);
        return Enumerable.Range(0, max).Select(i => points[(int)Math.Round(i * step)]).ToList();
    }
}
