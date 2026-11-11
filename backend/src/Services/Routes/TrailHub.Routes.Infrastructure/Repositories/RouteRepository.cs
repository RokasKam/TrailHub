using Microsoft.EntityFrameworkCore;
using TrailHub.Routes.Application.Interfaces.Repository;
using TrailHub.Routes.Application.Responses;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Repositories;

internal sealed class RouteRepository(RoutesDbContext db) : IRouteRepository
{
    public Task<PlannedRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RouteSummaryResponse>> GetSummariesVisibleToAsync(string userId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        // Skips the (large) routed line; waypoints are enough for the start point and day count.
        var rows = await db.Routes.AsNoTracking()
            .Where(r => r.OwnerId == userId || r.SharedWithGroups.Any(g => groupIds.Contains(g)))
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Select(r => new { r.Id, r.Name, r.OwnerId, r.DistanceMeters, r.AscentMeters, r.DurationMinutes, r.Snapped, r.UpdatedAtUtc, r.PublishedAtUtc, Waypoints = EF.Property<List<RouteWaypoint>>(r, "_waypoints") })
            .ToListAsync(cancellationToken);
        return rows
            .Select(r => new RouteSummaryResponse(
                r.Id,
                r.Name,
                r.DistanceMeters,
                r.AscentMeters,
                r.DurationMinutes,
                r.Waypoints.Count(w => w.IsOvernight) + 1,
                r.Snapped,
                r.Waypoints[0].Latitude,
                r.Waypoints[0].Longitude,
                r.UpdatedAtUtc,
                r.OwnerId,
                r.OwnerId == userId,
                r.PublishedAtUtc is not null))
            .ToList();
    }

    public void Add(PlannedRoute route) => db.Routes.Add(route);

    public void Remove(PlannedRoute route) => db.Routes.Remove(route);
}
