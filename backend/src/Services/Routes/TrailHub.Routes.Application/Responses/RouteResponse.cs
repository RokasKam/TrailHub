using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Responses;

/// <param name="Id">Null for a preview that hasn't been saved.</param>
/// <param name="IsOwner">Whether the viewer can edit, share and publish it; group members can only view, follow and copy.</param>
/// <param name="Path">The routed line as <c>[latitude, longitude, elevation]</c> triples (elevation may be null).</param>
/// <param name="Snapped">False when the line is straight between waypoints because no hiking path was found or routing is off.</param>
public sealed record RouteResponse(
    Guid? Id,
    string? Name,
    string? Description,
    IReadOnlyList<RouteWaypoint> Waypoints,
    IReadOnlyList<double?[]> Path,
    TrackStats Stats,
    IReadOnlyList<ElevationSample> ElevationProfile,
    IReadOnlyList<RouteDayResponse> Days,
    bool Snapped,
    IReadOnlyList<string> Warnings,
    DateTime? UpdatedAtUtc,
    bool IsOwner = true,
    IReadOnlyList<RouteGroupResponse>? SharedWith = null,
    DateTime? PublishedAtUtc = null);
