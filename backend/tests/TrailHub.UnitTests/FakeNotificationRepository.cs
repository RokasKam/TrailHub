using TrailHub.BuildingBlocks.Application;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.UnitTests;

internal sealed class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Items { get; } = [];

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(n => n.Id == id));

    public Task<Notification?> GetByKeyAsync(string userId, string groupKey, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(n => n.UserId == userId && n.GroupKey == groupKey));

    public Task<PagedResult<Notification>> GetPageAsync(string userId, PageRequest page, CancellationToken cancellationToken)
    {
        var mine = Items.Where(n => n.UserId == userId).OrderByDescending(n => n.UpdatedAtUtc).ToList();
        return Task.FromResult(new PagedResult<Notification>(mine.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, mine.Count));
    }

    public Task<int> CountUnreadAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(n => n.UserId == userId && !n.IsRead));

    public Task MarkAllReadAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        Items.Where(n => n.UserId == userId).ToList().ForEach(n => n.MarkRead(nowUtc));
        return Task.CompletedTask;
    }

    public void Add(Notification notification) => Items.Add(notification);
}
