namespace TrailHub.BuildingBlocks.Domain.Geo;

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
