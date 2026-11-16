using TrailHub.BuildingBlocks.Application;
using TrailHub.Notifications.Application.Responses;

namespace TrailHub.Notifications.Application.Interfaces.Service;

/// <summary>The caller's inbox.</summary>
public interface INotificationService
{
    Task<PagedResult<NotificationResponse>> GetPageAsync(PageRequest page, CancellationToken cancellationToken);

    Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken);

    /// <summary>Marks one of the caller's items read; others' ids are not found.</summary>
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken);

    Task MarkAllReadAsync(CancellationToken cancellationToken);
}
