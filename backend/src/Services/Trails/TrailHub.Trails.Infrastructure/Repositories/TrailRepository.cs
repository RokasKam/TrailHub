using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trails.Infrastructure.Persistence;

namespace TrailHub.Trails.Infrastructure.Repositories;

internal sealed class TrailRepository(TrailsDbContext db) : ITrailRepository
{
    private static readonly GeometryFactory Geography = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public Task<Trail?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Trails.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Trails.AnyAsync(t => t.Id == id, cancellationToken);

    public void Add(Trail trail) => db.Trails.Add(trail);

    public async Task<PagedResult<TrailSummaryResponse>> SearchAsync(TrailSearchCriteria c, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Trails.AsNoTracking().Where(t => t.HiddenBy == null); // hidden trails are out of Discover
        Point? origin = c.NearLatitude is { } lat && c.NearLongitude is { } lon
            ? Geography.CreatePoint(new Coordinate(lon, lat))
            : null;

        if (!string.IsNullOrWhiteSpace(c.Query))
        {
            var pattern = $"%{EscapeLike(c.Query.Trim())}%";
            query = query.Where(t => EF.Functions.ILike(t.Name, pattern, "\\") || EF.Functions.ILike(t.Region, pattern, "\\"));
        }

        if (origin is not null && c.RadiusKm is { } radiusKm)
        {
            query = query.Where(t => EF.Property<Point>(t, TrailsDbContext.StartLocation).IsWithinDistance(origin, radiusKm * 1000));
        }

        if (c.Bounds is { } b)
        {
            var box = Geography.CreatePolygon(
            [
                new Coordinate(b.MinLongitude, b.MinLatitude),
                new Coordinate(b.MaxLongitude, b.MinLatitude),
                new Coordinate(b.MaxLongitude, b.MaxLatitude),
                new Coordinate(b.MinLongitude, b.MaxLatitude),
                new Coordinate(b.MinLongitude, b.MinLatitude),
            ]);
            query = query.Where(t => EF.Property<LineString>(t, TrailsDbContext.Path).Intersects(box));
        }

        if (c.MinDistanceKm is { } minKm)
        {
            query = query.Where(t => t.DistanceMeters >= minKm * 1000);
        }

        if (c.MaxDistanceKm is { } maxKm)
        {
            query = query.Where(t => t.DistanceMeters <= maxKm * 1000);
        }

        if (c.Difficulties is { Count: > 0 } difficulties)
        {
            query = query.Where(t => difficulties.Contains(t.Difficulty));
        }

        if (c.MinElevationGainMeters is { } minGain)
        {
            query = query.Where(t => t.ElevationGainMeters >= minGain);
        }

        if (c.MaxElevationGainMeters is { } maxGain)
        {
            query = query.Where(t => t.ElevationGainMeters <= maxGain);
        }

        if (c.MinDurationMinutes is { } minDuration)
        {
            query = query.Where(t => t.EstimatedDurationMinutes >= minDuration);
        }

        if (c.MaxDurationMinutes is { } maxDuration)
        {
            query = query.Where(t => t.EstimatedDurationMinutes <= maxDuration);
        }

        var total = await query.CountAsync(cancellationToken);

        query = c.Sort switch
        {
            TrailSort.Distance or TrailSort.Relevance when origin is not null =>
                query.OrderBy(t => EF.Property<Point>(t, TrailsDbContext.StartLocation).Distance(origin)),
            TrailSort.Rating => query.OrderByDescending(t => t.AverageRating).ThenByDescending(t => t.ReviewCount),
            TrailSort.Length => query.OrderBy(t => t.DistanceMeters),
            TrailSort.Newest => query.OrderByDescending(t => t.CreatedAtUtc),
            _ => query.OrderByDescending(t => t.ReviewCount).ThenBy(t => t.Name),
        };

        var items = await Project(query.Skip(page.Skip).Take(page.PageSize), origin).ToListAsync(cancellationToken);
        return new PagedResult<TrailSummaryResponse>(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<TrailSummaryResponse>> GetSummariesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Project(db.Trails.AsNoTracking().Where(t => ids.Contains(t.Id) && t.HiddenBy == null).OrderBy(t => t.Name), origin: null).ToListAsync(cancellationToken);

    private static IQueryable<TrailSummaryResponse> Project(IQueryable<Trail> query, Point? origin) =>
        query.Select(t => new TrailSummaryResponse(
            t.Id,
            t.Name,
            t.Region,
            t.Difficulty,
            t.DistanceMeters,
            t.ElevationGainMeters,
            t.EstimatedDurationMinutes,
            t.IsLoop,
            new LocationResponse(EF.Property<double>(t, TrailsDbContext.StartLatitude), EF.Property<double>(t, TrailsDbContext.StartLongitude)),
            t.AverageRating,
            t.ReviewCount,
            origin == null ? null : EF.Property<Point>(t, TrailsDbContext.StartLocation).Distance(origin)));

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
