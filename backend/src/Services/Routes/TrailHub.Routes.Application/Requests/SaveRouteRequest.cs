namespace TrailHub.Routes.Application.Requests;

public sealed record SaveRouteRequest(string Name, string? Description, IReadOnlyList<WaypointRequest> Waypoints);
