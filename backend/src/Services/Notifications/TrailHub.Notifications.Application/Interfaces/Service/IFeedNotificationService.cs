using TrailHub.Contracts;

namespace TrailHub.Notifications.Application.Interfaces.Service;

/// <summary>Turns hike feed events (likes, comments, shares) into notifications.</summary>
public interface IFeedNotificationService
{
    Task HikeLikedAsync(HikeLikedIntegrationEvent e, CancellationToken cancellationToken);

    Task HikeCommentedAsync(HikeCommentedIntegrationEvent e, CancellationToken cancellationToken);

    Task HikeSharedAsync(HikeSharedIntegrationEvent e, CancellationToken cancellationToken);
}
