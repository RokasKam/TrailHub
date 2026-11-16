using TrailHub.BuildingBlocks.Application;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Application.Interfaces.Repository;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The user's item with this <see cref="Notification.GroupKey"/>, if any.</summary>
    Task<Notification?> GetByKeyAsync(string userId, string groupKey, CancellationToken cancellationToken);

    /// <summary>The user's items, most recently updated first.</summary>
    Task<PagedResult<Notification>> GetPageAsync(string userId, PageRequest page, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Marks every unread item of the user read (directly in the store).</summary>
    Task MarkAllReadAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken);

    void Add(Notification notification);
}
