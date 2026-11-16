using Microsoft.EntityFrameworkCore;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Domain.Notifications;
using TrailHub.Notifications.Infrastructure.Persistence;

namespace TrailHub.Notifications.Infrastructure.Repositories;

internal sealed class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<Notification?> GetByKeyAsync(string userId, string groupKey, CancellationToken cancellationToken) =>
        db.Notifications.Local.FirstOrDefault(n => n.UserId == userId && n.GroupKey == groupKey)
        ?? await db.Notifications.FirstOrDefaultAsync(n => n.UserId == userId && n.GroupKey == groupKey, cancellationToken);

    public async Task<PagedResult<Notification>> GetPageAsync(string userId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.UpdatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Notification>(items, page.Page, page.PageSize, total);
    }

    public Task<int> CountUnreadAsync(string userId, CancellationToken cancellationToken) =>
        db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, cancellationToken);

    public Task MarkAllReadAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.Notifications
            .Where(n => n.UserId == userId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, nowUtc), cancellationToken);

    public void Add(Notification notification) => db.Notifications.Add(notification);
}
