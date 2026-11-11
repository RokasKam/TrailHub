using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Requests;

public sealed record WaypointRequest(double Latitude, double Longitude, string? Name = null, bool IsOvernight = false, PoiKind? Kind = null)
{
    public RouteWaypoint ToWaypoint() => new(Latitude, Longitude, Name, IsOvernight, Kind);
}
