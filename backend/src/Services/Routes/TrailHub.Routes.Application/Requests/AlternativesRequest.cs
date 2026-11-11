namespace TrailHub.Routes.Application.Requests;

public sealed record AlternativesRequest(WaypointRequest From, WaypointRequest To);
