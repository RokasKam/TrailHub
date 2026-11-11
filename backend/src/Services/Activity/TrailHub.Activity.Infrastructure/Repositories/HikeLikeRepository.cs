using Microsoft.EntityFrameworkCore;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Domain.Feed;
using TrailHub.Activity.Infrastructure.Persistence;

namespace TrailHub.Activity.Infrastructure.Repositories;

internal sealed class HikeLikeRepository(ActivityDbContext db) : IHikeLikeRepository
{
    public Task<HikeLike?> GetAsync(Guid activityId, string userId, CancellationToken cancellationToken) =>
        db.HikeLikes.FirstOrDefaultAsync(l => l.ActivityId == activityId && l.UserId == userId, cancellationToken);

    public void Add(HikeLike like) => db.HikeLikes.Add(like);

    public void Remove(HikeLike like) => db.HikeLikes.Remove(like);

    public async Task<IReadOnlyDictionary<Guid, int>> GetCountsAsync(IReadOnlyCollection<Guid> activityIds, CancellationToken cancellationToken)
    {
        var ids = activityIds.ToArray();
        return await db.HikeLikes
            .Where(l => ids.Contains(l.ActivityId))
            .GroupBy(l => l.ActivityId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetLikedAsync(IReadOnlyCollection<Guid> activityIds, string userId, CancellationToken cancellationToken)
    {
        var ids = activityIds.ToArray();
        return (await db.HikeLikes
            .Where(l => l.UserId == userId && ids.Contains(l.ActivityId))
            .Select(l => l.ActivityId)
            .ToListAsync(cancellationToken))
            .ToHashSet();
    }
}
