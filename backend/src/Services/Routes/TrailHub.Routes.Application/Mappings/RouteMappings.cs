using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Application.Responses;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Mappings;

internal static class RouteMappings
{
    public static RouteResponse ToResponse(this PlannedRoute route, string viewerId, IReadOnlyList<GroupInfo> sharedWith) =>
        Build(route.Id, route.Name, route.Description, route.Waypoints, route.ToTrack(), route.WaypointIndices, route.Snapped, [], route.UpdatedAtUtc) with
        {
            IsOwner = route.OwnerId == viewerId,
            SharedWith = sharedWith.Select(g => new RouteGroupResponse(g.Id, g.Name)).ToList(),
            PublishedAtUtc = route.PublishedAtUtc,
        };

    public static RouteResponse Preview(IReadOnlyList<RouteWaypoint> waypoints, RoutedTrack routed, IReadOnlyList<string>? warnings = null) =>
        Build(null, null, null, waypoints, routed.Track, routed.WaypointIndices, routed.Snapped, warnings ?? [], null);

    private static RouteResponse Build(
        Guid? id, string? name, string? description, IReadOnlyList<RouteWaypoint> waypoints, Track track, IReadOnlyList<int> indices, bool snapped, IReadOnlyList<string> warnings, DateTime? updatedAtUtc)
    {
        var days = PlannedRoute.SplitIntoDays(waypoints, indices, track.LastIndex)
            .Select((d, i) => new RouteDayResponse(i + 1, track.DistanceAt(d.FromIndex), track.Measure(d.FromIndex, d.ToIndex), d.Overnight))
            .ToList();

        var allWarnings = warnings.ToList();
        if (!snapped)
        {
            allWarnings.Insert(0, "Shown as straight lines between waypoints, not along paths, so distance and time are rough.");
        }

        return new RouteResponse(
            id,
            name,
            description,
            waypoints,
            track.Points.Select(p => new[] { Math.Round(p.Latitude, 6), Math.Round(p.Longitude, 6), p.Elevation is { } e ? Math.Round(e, 1) : (double?)null }).ToList(),
            track.Total,
            ElevationProfile.Build(track.Points),
            days,
            snapped,
            allWarnings,
            updatedAtUtc);
    }
}
