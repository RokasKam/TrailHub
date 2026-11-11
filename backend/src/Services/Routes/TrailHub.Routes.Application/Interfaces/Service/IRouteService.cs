using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;

namespace TrailHub.Routes.Application.Interfaces.Service;

/// <summary>Saved routes: listing, reading, saving, deleting and GPX export.</summary>
public interface IRouteService
{
    /// <summary>The caller's routes and routes shared with their groups.</summary>
    Task<IReadOnlyList<RouteSummaryResponse>> GetRoutesAsync(CancellationToken cancellationToken);

    /// <summary>A route the caller can see: their own or one shared with one of their groups.</summary>
    Task<RouteResponse> GetRouteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Creates a route, routing the waypoints again.</summary>
    Task<RouteResponse> CreateAsync(SaveRouteRequest request, CancellationToken cancellationToken);

    /// <summary>Replaces one of the caller's routes, routing the waypoints again.</summary>
    Task<RouteResponse> UpdateAsync(Guid id, SaveRouteRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>GPX with the routed track, every waypoint (overnight stops marked), and optionally water, huts etc. along the way.</summary>
    Task<GpxFileResponse> ExportGpxAsync(Guid id, bool includePois, CancellationToken cancellationToken);
}
