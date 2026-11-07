using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Routes;

/// <param name="StartDistanceMeters">Where along the route the day starts, for marking days on the elevation chart.</param>
/// <param name="Overnight">Where the day ends; null on the last day.</param>
public sealed record RouteDayDto(int Day, double StartDistanceMeters, TrackStats Stats, RouteWaypoint? Overnight);

/// <summary>A group the route is shared with.</summary>
public sealed record RouteGroupDto(Guid Id, string Name);

/// <param name="Id">Null for a preview that hasn't been saved.</param>
/// <param name="IsOwner">Whether the viewer can edit, share and publish it; group members can only view, follow and copy.</param>
/// <param name="Path">The routed line as <c>[latitude, longitude, elevation]</c> triples (elevation may be null).</param>
/// <param name="Snapped">False when the line is straight between waypoints because no hiking path was found or routing is off.</param>
public sealed record RouteDto(
    Guid? Id,
    string? Name,
    string? Description,
    IReadOnlyList<RouteWaypoint> Waypoints,
    IReadOnlyList<double?[]> Path,
    TrackStats Stats,
    IReadOnlyList<ElevationSample> ElevationProfile,
    IReadOnlyList<RouteDayDto> Days,
    bool Snapped,
    IReadOnlyList<string> Warnings,
    DateTime? UpdatedAtUtc,
    bool IsOwner = true,
    IReadOnlyList<RouteGroupDto>? SharedWith = null,
    DateTime? PublishedAtUtc = null);

public sealed record WaypointInput(double Latitude, double Longitude, string? Name = null, bool IsOvernight = false, PoiKind? Kind = null)
{
    public RouteWaypoint ToWaypoint() => new(Latitude, Longitude, Name, IsOvernight, Kind);
}

internal static class RouteViews
{
    public static RouteDto ToDto(this PlannedRoute route, string viewerId, IReadOnlyList<GroupInfo> sharedWith) =>
        Build(route.Id, route.Name, route.Description, route.Waypoints, route.ToTrack(), route.WaypointIndices, route.Snapped, [], route.UpdatedAtUtc) with
        {
            IsOwner = route.OwnerId == viewerId,
            SharedWith = sharedWith.Select(g => new RouteGroupDto(g.Id, g.Name)).ToList(),
            PublishedAtUtc = route.PublishedAtUtc,
        };

    public static RouteDto Preview(IReadOnlyList<RouteWaypoint> waypoints, RoutedTrack routed, IReadOnlyList<string>? warnings = null) =>
        Build(null, null, null, waypoints, routed.Track, routed.WaypointIndices, routed.Snapped, warnings ?? [], null);

    private static RouteDto Build(
        Guid? id, string? name, string? description, IReadOnlyList<RouteWaypoint> waypoints, Track track, IReadOnlyList<int> indices, bool snapped, IReadOnlyList<string> warnings, DateTime? updatedAtUtc)
    {
        var days = PlannedRoute.SplitIntoDays(waypoints, indices, track.LastIndex)
            .Select((d, i) => new RouteDayDto(i + 1, track.DistanceAt(d.FromIndex), track.Measure(d.FromIndex, d.ToIndex), d.Overnight))
            .ToList();

        var allWarnings = warnings.ToList();
        if (!snapped)
        {
            allWarnings.Insert(0, "Shown as straight lines between waypoints, not along paths, so distance and time are rough.");
        }

        return new RouteDto(
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

internal static class RouteAccess
{
    /// <summary>
    /// A route the user may see: their own, or one shared with a group they're in. Anything else is reported as
    /// missing so ids can't be probed. Also returns the groups it's shared with (that still exist), for the DTO.
    /// </summary>
    public static async Task<(PlannedRoute Route, IReadOnlyList<GroupInfo> SharedWith)?> LoadVisibleAsync(
        this IRouteRepository routes, IGroupDirectory directory, Guid routeId, string userId, CancellationToken cancellationToken)
    {
        if (await routes.GetByIdAsync(routeId, cancellationToken) is not { } route)
        {
            return null;
        }

        var groups = new List<GroupInfo>();
        foreach (var groupId in route.SharedWithGroups)
        {
            if (await directory.GetAsync(groupId, cancellationToken) is { } group)
            {
                groups.Add(group);
            }
        }

        return route.OwnerId == userId || groups.Any(g => g.IsMember(userId)) ? (route, groups) : null;
    }
}

internal static class Routing
{
    /// <summary>Validates the waypoints and routes them along hiking paths.</summary>
    public static async Task<Result<(List<RouteWaypoint> Waypoints, RoutedTrack Routed)>> RouteWaypointsAsync(
        this IRouter router, IReadOnlyList<WaypointInput> input, CancellationToken cancellationToken)
    {
        var waypoints = input.Select(w => w.ToWaypoint()).ToList();
        if (PlannedRoute.ValidateWaypoints(waypoints) is { } invalid)
        {
            return invalid;
        }

        var routed = await router.RouteAsync(waypoints.Select(w => new GeoPoint(w.Latitude, w.Longitude)).ToList(), cancellationToken);
        return routed.IsSuccess ? (waypoints, routed.Value) : routed.Error!;
    }
}

/// <summary>Routes waypoints without saving, for the editor to show the line and numbers as the user places points.</summary>
public sealed record PreviewRouteQuery(IReadOnlyList<WaypointInput> Waypoints) : IQuery<RouteDto>;

internal sealed class PreviewRouteHandler(IRouter router) : IQueryHandler<PreviewRouteQuery, RouteDto>
{
    public async Task<Result<RouteDto>> Handle(PreviewRouteQuery query, CancellationToken cancellationToken)
    {
        var routed = await router.RouteWaypointsAsync(query.Waypoints, cancellationToken);
        return routed.IsSuccess ? RouteViews.Preview(routed.Value.Waypoints, routed.Value.Routed) : routed.Error!;
    }
}

public sealed record SaveRouteCommand(Guid? RouteId, string Name, string? Description, IReadOnlyList<WaypointInput> Waypoints) : ICommand<RouteDto>;

/// <summary>Creates a route (<c>RouteId = null</c>) or replaces one of the caller's routes, routing the waypoints again.</summary>
internal sealed class SaveRouteHandler(IRouter router, IRouteRepository routes, IGroupDirectory directory, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<SaveRouteCommand, RouteDto>
{
    public async Task<Result<RouteDto>> Handle(SaveRouteCommand command, CancellationToken cancellationToken)
    {
        PlannedRoute? existing = null;
        if (command.RouteId is { } id)
        {
            existing = await routes.GetByIdAsync(id, cancellationToken);
            if (existing is null || existing.OwnerId != user.UserId)
            {
                return RouteErrors.NotFound;
            }
        }

        var routed = await router.RouteWaypointsAsync(command.Waypoints, cancellationToken);
        if (routed.IsFailure)
        {
            return routed.Error!;
        }

        var (waypoints, track) = routed.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        if (existing is null)
        {
            var created = PlannedRoute.Create(user.UserId, command.Name, command.Description, waypoints, track, now);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            routes.Add(created.Value);
            existing = created.Value;
        }
        else if (existing.Update(command.Name, command.Description, waypoints, track, now) is { IsFailure: true } failed)
        {
            return failed.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await routes.LoadVisibleAsync(directory, existing.Id, user.UserId, cancellationToken) is (PlannedRoute saved, var groups)
            ? saved.ToDto(user.UserId, groups)
            : existing.ToDto(user.UserId, []);
    }
}

public sealed record DeleteRouteCommand(Guid RouteId) : ICommand;

internal sealed class DeleteRouteHandler(IRouteRepository routes, IUnitOfWork unitOfWork, ICurrentUser user) : ICommandHandler<DeleteRouteCommand>
{
    public async Task<Result> Handle(DeleteRouteCommand command, CancellationToken cancellationToken)
    {
        var route = await routes.GetByIdAsync(command.RouteId, cancellationToken);
        if (route is null || route.OwnerId != user.UserId)
        {
            return RouteErrors.NotFound;
        }

        routes.Remove(route);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GetRouteQuery(Guid RouteId) : IQuery<RouteDto>;

internal sealed class GetRouteHandler(IRouteRepository routes, IGroupDirectory directory, ICurrentUser user) : IQueryHandler<GetRouteQuery, RouteDto>
{
    public async Task<Result<RouteDto>> Handle(GetRouteQuery query, CancellationToken cancellationToken) =>
        await routes.LoadVisibleAsync(directory, query.RouteId, user.UserId, cancellationToken) is (PlannedRoute route, var groups)
            ? route.ToDto(user.UserId, groups)
            : RouteErrors.NotFound;
}

public sealed record GetMyRoutesQuery : IQuery<IReadOnlyList<RouteSummary>>;

/// <summary>The caller's routes and routes shared with their groups.</summary>
internal sealed class GetMyRoutesHandler(IRouteRepository routes, IGroupDirectory directory, ICurrentUser user) : IQueryHandler<GetMyRoutesQuery, IReadOnlyList<RouteSummary>>
{
    public async Task<Result<IReadOnlyList<RouteSummary>>> Handle(GetMyRoutesQuery query, CancellationToken cancellationToken)
    {
        var groups = await directory.GetForMemberAsync(user.UserId, cancellationToken);
        return Result<IReadOnlyList<RouteSummary>>.Success(await routes.GetSummariesVisibleToAsync(user.UserId, groups.Select(g => g.Id).ToList(), cancellationToken));
    }
}

/// <summary>GPX with the routed track, every waypoint (overnight stops marked), and optionally water, huts etc. along the way.</summary>
public sealed record ExportRouteGpxQuery(Guid RouteId, bool IncludePois) : IQuery<(string FileName, string Gpx)>;

internal sealed class ExportRouteGpxHandler(IRouteRepository routes, IGroupDirectory directory, IPoiSource pois, IGpxSerializer gpx, ICurrentUser user)
    : IQueryHandler<ExportRouteGpxQuery, (string FileName, string Gpx)>
{
    public async Task<Result<(string FileName, string Gpx)>> Handle(ExportRouteGpxQuery query, CancellationToken cancellationToken)
    {
        if (await routes.LoadVisibleAsync(directory, query.RouteId, user.UserId, cancellationToken) is not (PlannedRoute route, _))
        {
            return RouteErrors.NotFound;
        }

        var waypoints = route.Waypoints
            .Select((w, i) => new GpxWaypoint(
                w.Name ?? (i == 0 ? "Start" : i == route.Waypoints.Count - 1 ? "Finish" : $"Waypoint {i + 1}"),
                w.IsOvernight ? "Overnight" : "Waypoint",
                w.IsOvernight ? $"End of day {route.Waypoints.Take(i + 1).Count(x => x.IsOvernight)}" : null,
                w.Latitude,
                w.Longitude,
                route.Points[route.WaypointIndices[i]].Elevation))
            .ToList();

        if (query.IncludePois)
        {
            // Best effort: a route exports even if the map data service is down.
            var found = await pois.AlongAsync(route.Points, 500, Enum.GetValues<PoiKind>(), cancellationToken);
            if (found.IsSuccess)
            {
                waypoints.AddRange(found.Value.Select(p => new GpxWaypoint(p.Name, p.Kind.ToString(), null, p.Latitude, p.Longitude, p.Elevation)));
            }
        }

        // One segment per day, so GPS devices that show segments show the stages.
        var segments = route.Days()
            .Select(d => (IReadOnlyList<GpxTrackPoint>)route.Points.Skip(d.FromIndex).Take(d.ToIndex - d.FromIndex + 1)
                .Select(p => new GpxTrackPoint(p.Latitude, p.Longitude, p.Elevation, null))
                .ToList())
            .ToList();

        var document = new GpxDocument(route.Name, route.Description, segments, waypoints);
        var fileName = $"{string.Concat(route.Name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')).Trim('-')}.gpx";
        return (fileName, gpx.Write(document));
    }
}

/// <summary>Points of interest along a line, e.g. the route being edited. The line is thinned out before querying.</summary>
public sealed record PoisAlongQuery(IReadOnlyList<double[]> Line, IReadOnlyCollection<PoiKind>? Kinds, double? CorridorMeters) : IQuery<IReadOnlyList<Poi>>;

internal sealed class PoisAlongHandler(IPoiSource pois) : IQueryHandler<PoisAlongQuery, IReadOnlyList<Poi>>
{
    public const double MaxCorridorMeters = 2000;

    public Task<Result<IReadOnlyList<Poi>>> Handle(PoisAlongQuery query, CancellationToken cancellationToken)
    {
        var line = query.Line.Where(p => p.Length >= 2).Select(p => GeoPoint.Create(p[0], p[1])).Where(p => p.IsSuccess).Select(p => p.Value).ToList();
        if (line.Count == 0)
        {
            return Task.FromResult<Result<IReadOnlyList<Poi>>>(RouteErrors.InvalidWaypoint);
        }

        var kinds = query.Kinds is { Count: > 0 } k ? k : Enum.GetValues<PoiKind>();
        return pois.AlongAsync(line, Math.Clamp(query.CorridorMeters ?? 500, 50, MaxCorridorMeters), kinds, cancellationToken);
    }
}

public sealed record SearchPlacesQuery(string Text, double? NearLatitude, double? NearLongitude) : IQuery<IReadOnlyList<Place>>;

internal sealed class SearchPlacesHandler(IGeocoder geocoder) : IQueryHandler<SearchPlacesQuery, IReadOnlyList<Place>>
{
    public Task<Result<IReadOnlyList<Place>>> Handle(SearchPlacesQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Text) || query.Text.Length > 200)
        {
            return Task.FromResult<Result<IReadOnlyList<Place>>>(RouteErrors.PlaceNotFound);
        }

        GeoPoint? near = query is { NearLatitude: { } lat, NearLongitude: { } lon } && GeoPoint.Create(lat, lon) is { IsSuccess: true } p ? p.Value : null;
        return geocoder.SearchAsync(query.Text.Trim(), near, cancellationToken);
    }
}

/// <summary>A name for an unnamed route: "Chamonix → Argentière", or "Loop from Chamonix" when it ends where it starts.</summary>
public sealed record SuggestRouteNameQuery(IReadOnlyList<WaypointInput> Waypoints) : IQuery<string>;

internal sealed class SuggestRouteNameHandler(IGeocoder geocoder) : IQueryHandler<SuggestRouteNameQuery, string>
{
    public async Task<Result<string>> Handle(SuggestRouteNameQuery query, CancellationToken cancellationToken)
    {
        if (query.Waypoints.Count < 2)
        {
            return RouteErrors.WaypointCount;
        }

        async Task<string?> NameOf(WaypointInput w) =>
            w.Name ?? (await geocoder.ReverseAsync(new GeoPoint(w.Latitude, w.Longitude), cancellationToken))?.Name;

        var (first, last) = (query.Waypoints[0], query.Waypoints[^1]);
        var start = await NameOf(first);
        var loop = RouteMetrics.HaversineMeters(new GeoPoint(first.Latitude, first.Longitude), new GeoPoint(last.Latitude, last.Longitude)) < 300;
        if (loop)
        {
            return start is null ? "Loop" : $"Loop from {start}";
        }

        var end = await NameOf(last);
        return (start, end) switch
        {
            (null, null) => "New route",
            ({ } s, null) => $"From {s}",
            (null, { } e) => $"To {e}",
            var (s, e) when s == e => $"Around {s}",
            var (s, e) => $"{s} → {e}",
        };
    }
}
