using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Interfaces.Providers;

/// <summary>Finds walkable lines between points (OpenRouteService's hiking profile in production).</summary>
public interface IRouter
{
    /// <summary>A route through all waypoints, in order, following hiking paths.</summary>
    Task<Result<RoutedTrack>> RouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken);

    /// <summary>Up to <paramref name="count"/> different routes between two points, best first.</summary>
    Task<Result<IReadOnlyList<RoutedTrack>>> AlternativesAsync(GeoPoint from, GeoPoint to, int count, CancellationToken cancellationToken);
}
