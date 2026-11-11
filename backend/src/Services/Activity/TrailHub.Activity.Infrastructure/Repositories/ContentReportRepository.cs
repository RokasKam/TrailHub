using Microsoft.EntityFrameworkCore;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Domain.Feed;
using TrailHub.Activity.Infrastructure.Persistence;

namespace TrailHub.Activity.Infrastructure.Repositories;

internal sealed class ContentReportRepository(ActivityDbContext db) : IContentReportRepository
{
    public Task<ContentReport?> GetByUserAsync(Guid contentId, string userId, CancellationToken cancellationToken) =>
        db.ContentReports.FirstOrDefaultAsync(r => r.ContentId == contentId && r.UserId == userId, cancellationToken);

    public Task<int> CountOpenAsync(Guid contentId, CancellationToken cancellationToken) =>
        db.ContentReports.CountAsync(r => r.ContentId == contentId && r.ResolvedAtUtc == null, cancellationToken);

    public async Task<IReadOnlyList<ContentReport>> GetOpenAsync(Guid contentId, CancellationToken cancellationToken) =>
        await db.ContentReports.Where(r => r.ContentId == contentId && r.ResolvedAtUtc == null).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentReport>> GetAllOpenAsync(CancellationToken cancellationToken) =>
        await db.ContentReports.AsNoTracking().Where(r => r.ResolvedAtUtc == null).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentReport>> GetAllAsync(Guid contentId, CancellationToken cancellationToken) =>
        await db.ContentReports.Where(r => r.ContentId == contentId).ToListAsync(cancellationToken);

    public void Add(ContentReport report) => db.ContentReports.Add(report);

    public void Remove(ContentReport report) => db.ContentReports.Remove(report);
}
