using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Services;

internal static class RouterExtensions
{
    /// <summary>Validates the waypoints and routes them along hiking paths.</summary>
    public static async Task<(List<RouteWaypoint> Waypoints, RoutedTrack Routed)> RouteWaypointsAsync(
        this IRouter router, IReadOnlyList<WaypointRequest> input, CancellationToken cancellationToken)
    {
        var waypoints = input.Select(w => w.ToWaypoint()).ToList();
        if (PlannedRoute.ValidateWaypoints(waypoints) is { } invalid)
        {
            throw invalid.ToException();
        }

        var routed = await router.RouteAsync(waypoints.Select(w => new GeoPoint(w.Latitude, w.Longitude)).ToList(), cancellationToken);
        return (waypoints, routed.GetValueOrThrow());
    }
}
