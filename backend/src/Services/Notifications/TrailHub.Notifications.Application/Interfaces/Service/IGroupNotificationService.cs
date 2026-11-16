using TrailHub.Contracts;

namespace TrailHub.Notifications.Application.Interfaces.Service;

/// <summary>Turns group events into notifications.</summary>
public interface IGroupNotificationService
{
    Task GroupHikeScheduledAsync(GroupHikeScheduledIntegrationEvent e, CancellationToken cancellationToken);
}
