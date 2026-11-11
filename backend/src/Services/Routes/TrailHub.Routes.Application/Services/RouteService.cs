using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Interfaces.Repository;
using TrailHub.Routes.Application.Interfaces.Service;
using TrailHub.Routes.Application.Mappings;
using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Services;

internal sealed class RouteService(
    IRouteRepository routes,
    IGroupDirectory directory,
    IRouter router,
    IPoiSource pois,
    IGpxSerializer gpx,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IRouteService
{
    public async Task<IReadOnlyList<RouteSummaryResponse>> GetRoutesAsync(CancellationToken cancellationToken)
    {
        var groups = await directory.GetForMemberAsync(user.UserId, cancellationToken);
        return await routes.GetSummariesVisibleToAsync(user.UserId, groups.Select(g => g.Id).ToList(), cancellationToken);
    }

    public async Task<RouteResponse> GetRouteAsync(Guid id, CancellationToken cancellationToken)
    {
        var (route, groups) = await routes.GetVisibleAsync(directory, id, user.UserId, cancellationToken);
        return route.ToResponse(user.UserId, groups);
    }

    public Task<RouteResponse> CreateAsync(SaveRouteRequest request, CancellationToken cancellationToken) =>
        SaveAsync(null, request, cancellationToken);

    public Task<RouteResponse> UpdateAsync(Guid id, SaveRouteRequest request, CancellationToken cancellationToken) =>
        SaveAsync(id, request, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var route = await GetOwnAsync(id, cancellationToken);
        routes.Remove(route);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<GpxFileResponse> ExportGpxAsync(Guid id, bool includePois, CancellationToken cancellationToken)
    {
        var (route, _) = await routes.GetVisibleAsync(directory, id, user.UserId, cancellationToken);

        var waypoints = route.Waypoints
            .Select((w, i) => new GpxWaypoint(
                w.Name ?? (i == 0 ? "Start" : i == route.Waypoints.Count - 1 ? "Finish" : $"Waypoint {i + 1}"),
                w.IsOvernight ? "Overnight" : "Waypoint",
                w.IsOvernight ? $"End of day {route.Waypoints.Take(i + 1).Count(x => x.IsOvernight)}" : null,
                w.Latitude,
                w.Longitude,
                route.Points[route.WaypointIndices[i]].Elevation))
            .ToList();

        if (includePois)
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
        return new GpxFileResponse(fileName, gpx.Write(document));
    }

    /// <summary>Creates a route (<paramref name="id"/> null) or replaces one of the caller's routes, routing the waypoints again.</summary>
    private async Task<RouteResponse> SaveAsync(Guid? id, SaveRouteRequest request, CancellationToken cancellationToken)
    {
        var existing = id is { } routeId ? await GetOwnAsync(routeId, cancellationToken) : null;

        var (waypoints, track) = await router.RouteWaypointsAsync(request.Waypoints, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (existing is null)
        {
            existing = PlannedRoute.Create(user.UserId, request.Name, request.Description, waypoints, track, now).GetValueOrThrow();
            routes.Add(existing);
        }
        else
        {
            existing.Update(request.Name, request.Description, waypoints, track, now).ThrowIfFailure();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await routes.LoadVisibleAsync(directory, existing.Id, user.UserId, cancellationToken) is (PlannedRoute saved, var groups)
            ? saved.ToResponse(user.UserId, groups)
            : existing.ToResponse(user.UserId, []);
    }

    /// <summary>One of the caller's own routes. Other people's routes are reported as not found.</summary>
    private async Task<PlannedRoute> GetOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        var route = await routes.GetByIdAsync(id, cancellationToken);
        return route is not null && route.OwnerId == user.UserId
            ? route
            : throw RouteErrors.NotFound.ToException();
    }
}
