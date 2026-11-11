using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Responses;

/// <param name="StartDistanceMeters">Where along the route the day starts, for marking days on the elevation chart.</param>
/// <param name="Overnight">Where the day ends; null on the last day.</param>
public sealed record RouteDayResponse(int Day, double StartDistanceMeters, TrackStats Stats, RouteWaypoint? Overnight);
