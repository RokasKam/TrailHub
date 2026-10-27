using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Application.Abstractions;
using TrailHub.Trails.Application.Trails;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Persistence;

internal sealed class TrailRepository(TrailsDbContext db) : ITrailRepository
{
    private static readonly GeometryFactory Geography = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public Task<Trail?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Trails.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Trails.AnyAsync(t => t.Id == id, cancellationToken);

    public void Add(Trail trail) => db.Trails.Add(trail);

    public async Task<PagedResult<TrailSummaryDto>> SearchAsync(TrailSearchCriteria c, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Trails.AsNoTracking();
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
        return new PagedResult<TrailSummaryDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<TrailSummaryDto>> GetSummariesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Project(db.Trails.AsNoTracking().Where(t => ids.Contains(t.Id)).OrderBy(t => t.Name), origin: null).ToListAsync(cancellationToken);

    private static IQueryable<TrailSummaryDto> Project(IQueryable<Trail> query, Point? origin) =>
        query.Select(t => new TrailSummaryDto(
            t.Id,
            t.Name,
            t.Region,
            t.Difficulty,
            t.DistanceMeters,
            t.ElevationGainMeters,
            t.EstimatedDurationMinutes,
            t.IsLoop,
            new LocationDto(EF.Property<double>(t, TrailsDbContext.StartLatitude), EF.Property<double>(t, TrailsDbContext.StartLongitude)),
            t.AverageRating,
            t.ReviewCount,
            origin == null ? null : EF.Property<Point>(t, TrailsDbContext.StartLocation).Distance(origin)));

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

internal sealed class ReviewRepository(TrailsDbContext db) : IReviewRepository
{
    public Task<Review?> GetByUserAsync(Guid trailId, string userId, CancellationToken cancellationToken) =>
        db.Reviews.FirstOrDefaultAsync(r => r.TrailId == trailId && r.UserId == userId, cancellationToken);

    public void Add(Review review) => db.Reviews.Add(review);

    public async Task<PagedResult<Review>> GetPageAsync(Guid trailId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Reviews.AsNoTracking().Where(r => r.TrailId == trailId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.CreatedAtUtc).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<Review>(items, page.Page, page.PageSize, total);
    }

    public async Task<(int Sum, int Count)> GetRatingTotalsAsync(Guid trailId, string excludingUserId, CancellationToken cancellationToken)
    {
        var totals = await db.Reviews
            .Where(r => r.TrailId == trailId && r.UserId != excludingUserId)
            .GroupBy(_ => 1)
            .Select(g => new { Sum = g.Sum(r => r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);
        return totals is null ? (0, 0) : (totals.Sum, totals.Count);
    }
}

internal sealed class ConditionReportRepository(TrailsDbContext db) : IConditionReportRepository
{
    public void Add(ConditionReport report) => db.ConditionReports.Add(report);

    public async Task<IReadOnlyList<ConditionReport>> GetRecentAsync(Guid trailId, int take, CancellationToken cancellationToken) =>
        await db.ConditionReports.AsNoTracking()
            .Where(r => r.TrailId == trailId)
            .OrderByDescending(r => r.ReportedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
}

internal sealed class FavoriteTrailRepository(TrailsDbContext db) : IFavoriteTrailRepository
{
    public Task<FavoriteTrail?> GetAsync(string userId, Guid trailId, CancellationToken cancellationToken) =>
        db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.TrailId == trailId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetTrailIdsAsync(string userId, CancellationToken cancellationToken) =>
        await db.Favorites.Where(f => f.UserId == userId).OrderByDescending(f => f.SavedAtUtc).Select(f => f.TrailId).ToListAsync(cancellationToken);

    public void Add(FavoriteTrail favorite) => db.Favorites.Add(favorite);

    public void Remove(FavoriteTrail favorite) => db.Favorites.Remove(favorite);
}
