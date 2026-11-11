using Microsoft.EntityFrameworkCore;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Infrastructure.Persistence;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Activity.Infrastructure.Repositories;

internal sealed class ActivityRepository(ActivityDbContext db) : IActivityRepository
{
    public Task<HikeActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Activities.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<HikeActivity?> FindByPhotoIdAsync(Guid photoId, CancellationToken cancellationToken)
    {
        // Photos are a jsonb array of {"id": …, …}; containment is the indexable way to find the element.
        var match = $"[{{\"id\":\"{photoId}\"}}]";
        return db.Activities
            .FromSql($"SELECT * FROM activity.\"Activities\" WHERE \"Photos\" @> {match}::jsonb")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HikeActivity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var array = ids.ToArray();
        return await db.Activities.AsNoTracking().Where(a => array.Contains(a.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetIdsWithHiddenPhotosAsync(CancellationToken cancellationToken) =>
        await db.Database
            .SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM activity.\"Activities\" WHERE jsonb_path_exists(\"Photos\", '$[*] ? (@.hiddenBy != null)')")
            .ToListAsync(cancellationToken);

    public void Add(HikeActivity activity) => db.Activities.Add(activity);

    public void Remove(HikeActivity activity) => db.Activities.Remove(activity);

    public async Task<PagedResult<ActivitySummaryResponse>> GetPageAsync(string userId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Activities.AsNoTracking().Where(a => a.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.StartedAtUtc)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(a => new ActivitySummaryResponse(a.Id, a.Name, a.TrailId, a.Source, a.StartedAtUtc, a.EndedAtUtc, a.DistanceMeters, a.ElevationGainMeters, a.MovingDurationSeconds, a.AverageSpeedMetersPerSecond))
            .ToListAsync(cancellationToken);
        return new PagedResult<ActivitySummaryResponse>(items, page.Page, page.PageSize, total);
    }

    public async Task<PagedResult<HikeActivity>> GetFeedPageAsync(string userId, IReadOnlyCollection<Guid> groupIds, bool includeOwn, PageRequest page, CancellationToken cancellationToken)
    {
        var groups = groupIds.ToArray();
        var query = db.Activities.AsNoTracking()
            .Where(a => a.SharedAtUtc != null && (a.SharedGroupIds.Any(g => groups.Contains(g)) || (includeOwn && a.UserId == userId)));
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.SharedAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<HikeActivity>(items, page.Page, page.PageSize, total);
    }

    public async Task<ActivityTotalsResponse> GetTotalsAsync(string userId, DateTime? sinceUtc, CancellationToken cancellationToken)
    {
        var query = db.Activities.Where(a => a.UserId == userId);
        if (sinceUtc is { } since)
        {
            query = query.Where(a => a.StartedAtUtc >= since);
        }

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new ActivityTotalsResponse(
                g.Count(),
                g.Sum(a => a.DistanceMeters),
                g.Sum(a => a.ElevationGainMeters),
                g.Sum(a => a.MovingDurationSeconds),
                g.Max(a => a.DistanceMeters),
                g.Max(a => a.MaxElevationMeters)))
            .FirstOrDefaultAsync(cancellationToken);
        return totals ?? new ActivityTotalsResponse(0, 0, 0, 0, 0, null);
    }

    public async Task<IReadOnlyList<MonthlyStatisticsResponse>> GetMonthlyAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken) =>
        await db.Activities
            .Where(a => a.UserId == userId && a.StartedAtUtc >= sinceUtc)
            .GroupBy(a => new { a.StartedAtUtc.Year, a.StartedAtUtc.Month })
            .Select(g => new MonthlyStatisticsResponse(g.Key.Year, g.Key.Month, g.Count(), g.Sum(a => a.DistanceMeters), g.Sum(a => a.ElevationGainMeters)))
            .ToListAsync(cancellationToken);
}
