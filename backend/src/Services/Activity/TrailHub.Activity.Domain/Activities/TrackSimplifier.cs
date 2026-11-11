using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Domain.Activities;

/// <summary>Shrinks a track for previews while keeping its shape.</summary>
public static class TrackSimplifier
{
    private const double MetersPerDegreeLatitude = 110_574;
    private const double MetersPerDegreeLongitudeAtEquator = 111_320;

    /// <summary>
    /// Keeps at most <paramref name="maxPoints"/> points, always both ends, choosing the points that matter most to
    /// the shape (Douglas–Peucker, refining the span with the largest deviation first until the budget is used).
    /// Tracks that already fit are returned unchanged.
    /// </summary>
    public static IReadOnlyList<GeoPoint> Simplify(IReadOnlyList<GeoPoint> points, int maxPoints)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, 2);
        if (points.Count <= maxPoints)
        {
            return points;
        }

        // Local equirectangular projection to meters: accurate enough to rank deviations within one hike.
        var cosLatitude = Math.Cos(points[0].Latitude * Math.PI / 180);
        var x = points.Select(p => p.Longitude * MetersPerDegreeLongitudeAtEquator * cosLatitude).ToArray();
        var y = points.Select(p => p.Latitude * MetersPerDegreeLatitude).ToArray();

        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var kept = 2;

        var spans = new PriorityQueue<(int From, int To, int Farthest), double>(Comparer<double>.Create((a, b) => b.CompareTo(a)));
        Enqueue(0, points.Count - 1);
        while (kept < maxPoints && spans.TryDequeue(out var span, out var deviation) && deviation > 0)
        {
            keep[span.Farthest] = true;
            kept++;
            Enqueue(span.From, span.Farthest);
            Enqueue(span.Farthest, span.To);
        }

        return points.Where((_, i) => keep[i]).ToList();

        void Enqueue(int from, int to)
        {
            if (to - from < 2)
            {
                return;
            }

            var (farthest, max) = (from + 1, -1.0);
            for (var i = from + 1; i < to; i++)
            {
                var d = DistanceToSegment(x[i], y[i], x[from], y[from], x[to], y[to]);
                if (d > max)
                {
                    (farthest, max) = (i, d);
                }
            }

            spans.Enqueue((from, to, farthest), max);
        }
    }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var (dx, dy) = (bx - ax, by - ay);
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / lengthSquared, 0, 1);
        var (cx, cy) = (ax + t * dx - px, ay + t * dy - py);
        return Math.Sqrt(cx * cx + cy * cy);
    }
}
