namespace TrailHub.BuildingBlocks.Domain.Geo;

public sealed record RouteMetrics(
    double DistanceMeters,
    double ElevationGainMeters,
    double ElevationLossMeters,
    double? MinElevationMeters,
    double? MaxElevationMeters)
{
    /// <summary>
    /// Elevation changes smaller than this are treated as GPS/DEM noise. Without it, a flat
    /// track recorded on a phone can report hundreds of meters of phantom climbing.
    /// </summary>
    public const double ElevationNoiseThresholdMeters = 3;

    private const double EarthRadiusMeters = 6_371_008.8;

    public static RouteMetrics Calculate(IReadOnlyList<GeoPoint> points)
    {
        double distance = 0, gain = 0, loss = 0;
        double? min = null, max = null, anchor = null;

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (i > 0)
            {
                distance += HaversineMeters(points[i - 1], point);
            }

            if (point.Elevation is not { } elevation)
            {
                continue;
            }

            min = min is null ? elevation : Math.Min(min.Value, elevation);
            max = max is null ? elevation : Math.Max(max.Value, elevation);

            if (anchor is null)
            {
                anchor = elevation;
            }
            else if (elevation - anchor.Value >= ElevationNoiseThresholdMeters)
            {
                gain += elevation - anchor.Value;
                anchor = elevation;
            }
            else if (anchor.Value - elevation >= ElevationNoiseThresholdMeters)
            {
                loss += anchor.Value - elevation;
                anchor = elevation;
            }
        }

        return new RouteMetrics(distance, gain, loss, min, max);
    }

    /// <summary>Great-circle (haversine) distance between two points.</summary>
    public static double HaversineMeters(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLon = ToRadians(b.Longitude - a.Longitude);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRadians(a.Latitude)) * Math.Cos(ToRadians(b.Latitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
