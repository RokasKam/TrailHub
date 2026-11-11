using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;

namespace TrailHub.Routes.Application.Interfaces.Service;

/// <summary>Map lookups for the editor: points of interest along a line, place search and route names.</summary>
public interface IPlaceService
{
    /// <summary>Points of interest along a line, e.g. the route being edited. The line is thinned out before querying.</summary>
    Task<IReadOnlyList<Poi>> GetPoisAlongAsync(PoisAlongRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<Place>> SearchPlacesAsync(string text, double? nearLatitude, double? nearLongitude, CancellationToken cancellationToken);

    /// <summary>A name for an unnamed route: "Chamonix → Argentière", or "Loop from Chamonix" when it ends where it starts.</summary>
    Task<RouteNameResponse> SuggestNameAsync(IReadOnlyList<WaypointRequest> waypoints, CancellationToken cancellationToken);
}
