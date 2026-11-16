using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Application.Mappings;
using TrailHub.Notifications.Application.Responses;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Application.Services;

internal sealed class NotificationService(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : INotificationService
{
    public async Task<PagedResult<NotificationResponse>> GetPageAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var result = await notifications.GetPageAsync(user.UserId, page, cancellationToken);
        return new PagedResult<NotificationResponse>(result.Items.Select(n => n.ToResponse()).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken) =>
        new(await notifications.CountUnreadAsync(user.UserId, cancellationToken));

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null || notification.UserId != user.UserId)
        {
            throw NotificationErrors.NotFound.ToException();
        }

        notification.MarkRead(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task MarkAllReadAsync(CancellationToken cancellationToken) =>
        notifications.MarkAllReadAsync(user.UserId, clock.GetUtcNow().UtcDateTime, cancellationToken);
}
