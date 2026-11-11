namespace TrailHub.Routes.Domain.Routes;

/// <summary>A point the user placed. Overnight waypoints end a day, which is how a route becomes multi-day.</summary>
/// <param name="Kind">What the stop is, when it came from the map (e.g. the hut the planner chose).</param>
public sealed record RouteWaypoint(double Latitude, double Longitude, string? Name, bool IsOvernight, PoiKind? Kind = null);
