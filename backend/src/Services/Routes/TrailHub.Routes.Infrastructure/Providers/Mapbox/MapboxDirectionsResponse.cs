namespace TrailHub.Routes.Infrastructure.Providers.Mapbox;

internal sealed record MapboxDirectionsResponse(string? Code, string? Message, IReadOnlyList<MapboxDirectionsRoute>? Routes, IReadOnlyList<MapboxDirectionsWaypoint>? Waypoints);
