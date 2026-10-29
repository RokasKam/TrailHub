using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Routes.Domain.Routes;

/// <summary>Distance, climbing and walking time for part of a track.</summary>
/// <param name="MaxSacScale">Hardest trail grade on it: 1 = T1 hiking … 6 = T6 difficult alpine; 0 = unknown.</param>
public sealed record TrackStats(
    double DistanceMeters,
    double AscentMeters,
    double DescentMeters,
    double? MinElevationMeters,
    double? MaxElevationMeters,
    int DurationMinutes,
    int MaxSacScale);

/// <summary>
/// A routed line with elevation, plus running totals so any stretch of it (a day, a leg) can be measured
/// without walking the points again. Immutable.
/// </summary>
public sealed class Track
{
    private readonly double[] _distance;
    private readonly double[] _ascent;
    private readonly double[] _descent;
    private readonly double[] _terrain; // running sum of distance × terrain factor

    /// <param name="sacScale">Trail grade per point (0 = unknown); empty when the router doesn't say.</param>
    public Track(IReadOnlyList<GeoPoint> points, IReadOnlyList<byte>? sacScale = null)
    {
        if (points.Count < 2)
        {
            throw new ArgumentException("A track needs at least two points.", nameof(points));
        }

        Points = points;
        SacScale = sacScale is { } grades && grades.Count == points.Count ? grades : new byte[points.Count];
        _distance = new double[points.Count];
        _ascent = new double[points.Count];
        _descent = new double[points.Count];
        _terrain = new double[points.Count];

        // Same noise filter as RouteMetrics: ignore wiggles under the threshold so DEM noise doesn't add phantom climbing.
        double? anchor = points[0].Elevation;
        for (var i = 1; i < points.Count; i++)
        {
            var step = RouteMetrics.HaversineMeters(points[i - 1], points[i]);
            _distance[i] = _distance[i - 1] + step;
            _terrain[i] = _terrain[i - 1] + (step * HikingTime.TerrainFactor(SacScale[i]));
            _ascent[i] = _ascent[i - 1];
            _descent[i] = _descent[i - 1];
            if (points[i].Elevation is not { } elevation)
            {
                continue;
            }

            if (anchor is null)
            {
                anchor = elevation;
            }
            else if (elevation - anchor.Value >= RouteMetrics.ElevationNoiseThresholdMeters)
            {
                _ascent[i] += elevation - anchor.Value;
                anchor = elevation;
            }
            else if (anchor.Value - elevation >= RouteMetrics.ElevationNoiseThresholdMeters)
            {
                _descent[i] += anchor.Value - elevation;
                anchor = elevation;
            }
        }
    }

    public IReadOnlyList<GeoPoint> Points { get; }

    public IReadOnlyList<byte> SacScale { get; }

    public int LastIndex => Points.Count - 1;

    public double DistanceAt(int index) => _distance[index];

    public TrackStats Total => Measure(0, LastIndex);

    /// <summary>Stats for the stretch from point <paramref name="from"/> to point <paramref name="to"/>.</summary>
    public TrackStats Measure(int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(from, to);
        var distance = _distance[to] - _distance[from];
        var ascent = _ascent[to] - _ascent[from];
        var descent = _descent[to] - _descent[from];

        double? min = null, max = null;
        var maxSac = 0;
        for (var i = from; i <= to; i++)
        {
            if (Points[i].Elevation is { } e)
            {
                min = min is null ? e : Math.Min(min.Value, e);
                max = max is null ? e : Math.Max(max.Value, e);
            }

            maxSac = Math.Max(maxSac, SacScale[i]);
        }

        return new TrackStats(distance, ascent, descent, min, max, Minutes(from, to), maxSac);
    }

    /// <summary>Walking time for a stretch in O(1), for planners that measure thousands of candidate days.</summary>
    public int Minutes(int from, int to, double extraMeters = 0)
    {
        var distance = _distance[to] - _distance[from];
        var factor = distance > 0 ? (_terrain[to] - _terrain[from]) / distance : 1;
        return HikingTime.EstimateMinutes(distance + extraMeters, _ascent[to] - _ascent[from], _descent[to] - _descent[from], factor);
    }

    /// <summary>The track point closest to <paramref name="location"/> and how far away it is.</summary>
    public (int Index, double DistanceMeters) Nearest(GeoPoint location)
    {
        var best = (Index: 0, DistanceMeters: double.MaxValue);
        for (var i = 0; i < Points.Count; i++)
        {
            var d = RouteMetrics.HaversineMeters(Points[i], location);
            if (d < best.DistanceMeters)
            {
                best = (i, d);
            }
        }

        return best;
    }

    /// <summary>The first point at or after <paramref name="meters"/> along the track.</summary>
    public int IndexAtDistance(double meters)
    {
        var i = Array.BinarySearch(_distance, meters);
        return Math.Clamp(i >= 0 ? i : ~i, 0, LastIndex);
    }
}
