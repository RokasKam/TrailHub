using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;

namespace TrailHub.Routes.Application.Interfaces.Service;

/// <summary>Routing without saving: previews, alternatives and the multi-day planner.</summary>
public interface IRoutePlanningService
{
    /// <summary>Routes waypoints without saving, for the editor to show the line and numbers as the user places points.</summary>
    Task<RouteResponse> PreviewAsync(IReadOnlyList<WaypointRequest> waypoints, CancellationToken cancellationToken);

    /// <summary>Up to three different ways between two points, best first, so the user can pick one.</summary>
    Task<IReadOnlyList<RouteResponse>> FindAlternativesAsync(WaypointRequest from, WaypointRequest to, CancellationToken cancellationToken);

    /// <summary>
    /// The multi-day planner: routes start → finish, looks up places to sleep along the way, picks a stop for each
    /// night (<see cref="Domain.Routes.ItineraryPlanner"/>), then routes again through those stops so each day really ends at the bed.
    /// The answer is an unsaved route whose overnight waypoints are the stops; saving it is a normal route save.
    /// </summary>
    Task<RouteResponse> PlanItineraryAsync(PlanItineraryRequest request, CancellationToken cancellationToken);
}
