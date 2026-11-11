using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>
/// The last resort when no routing service is configured: joins waypoints with straight lines sampled every
/// <see cref="SpacingMeters"/>, with ground elevation, so distance, climbing, time and the elevation profile are
/// still roughly right. Results are marked as not snapped.
/// </summary>
internal sealed class StraightLineRouter(IElevationSource elevation) : IRouter
{
    private const double SpacingMeters = 100;
    private const int MaxPoints = 400;

    public async Task<Result<RoutedTrack>> RouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        var total = waypoints.Zip(waypoints.Skip(1), RouteMetrics.HaversineMeters).Sum();
        var spacing = Math.Max(SpacingMeters, total / MaxPoints);

        var points = new List<GeoPoint> { waypoints[0] };
        var indices = new List<int> { 0 };
        foreach (var (a, b) in waypoints.Zip(waypoints.Skip(1)))
        {
            var steps = Math.Max(1, (int)Math.Ceiling(RouteMetrics.HaversineMeters(a, b) / spacing));
            for (var s = 1; s <= steps; s++)
            {
                var t = s / (double)steps;
                points.Add(new GeoPoint(a.Latitude + ((b.Latitude - a.Latitude) * t), a.Longitude + ((b.Longitude - a.Longitude) * t)));
            }

            indices.Add(points.Count - 1);
        }

        return new RoutedTrack(new Track(await elevation.TryAddElevationAsync(points, cancellationToken) ?? points), indices, Snapped: false);
    }

    public async Task<Result<IReadOnlyList<RoutedTrack>>> AlternativesAsync(GeoPoint from, GeoPoint to, int count, CancellationToken cancellationToken)
    {
        var only = await RouteAsync([from, to], cancellationToken);
        return only.IsSuccess ? new[] { only.Value } : only.Error!;
    }
}
