namespace TrailHub.Routes.Application.Requests;

public sealed record WaypointsRequest(IReadOnlyList<WaypointRequest> Waypoints);
