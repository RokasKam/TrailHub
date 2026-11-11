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
