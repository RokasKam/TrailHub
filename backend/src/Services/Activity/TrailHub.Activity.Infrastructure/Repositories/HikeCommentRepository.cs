using Microsoft.EntityFrameworkCore;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Domain.Feed;
using TrailHub.Activity.Infrastructure.Persistence;

namespace TrailHub.Activity.Infrastructure.Repositories;

internal sealed class HikeCommentRepository(ActivityDbContext db) : IHikeCommentRepository
{
    public Task<HikeComment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.HikeComments.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<HikeComment>> GetForActivityAsync(Guid activityId, CancellationToken cancellationToken) =>
        await db.HikeComments.AsNoTracking()
            .Where(c => c.ActivityId == activityId)
            .OrderBy(c => c.CreatedAtUtc)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetCountsAsync(IReadOnlyCollection<Guid> activityIds, string viewerId, bool includeHidden, CancellationToken cancellationToken)
    {
        var ids = activityIds.ToArray();
        return await db.HikeComments
            .Where(c => ids.Contains(c.ActivityId) && (includeHidden || c.HiddenBy == null || c.UserId == viewerId))
            .GroupBy(c => c.ActivityId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }

    public async Task<IReadOnlyList<HikeComment>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var array = ids.ToArray();
        return await db.HikeComments.AsNoTracking().Where(c => array.Contains(c.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HikeComment>> GetHiddenAsync(CancellationToken cancellationToken) =>
        await db.HikeComments.AsNoTracking().Where(c => c.HiddenBy != null).ToListAsync(cancellationToken);

    public void Add(HikeComment comment) => db.HikeComments.Add(comment);

    public void Remove(HikeComment comment) => db.HikeComments.Remove(comment);
}
