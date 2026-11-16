using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Application.Services;

internal sealed class FeedNotificationService(
    NotificationDispatcher dispatcher,
    INotificationRepository notifications,
    IGroupDirectory directory) : IFeedNotificationService
{
    public async Task HikeLikedAsync(HikeLikedIntegrationEvent e, CancellationToken cancellationToken)
    {
        if (e.LikerId == e.OwnerId)
        {
            return;
        }

        // One item per hike: the first like creates it, later likes update it (and push at most every 10 minutes).
        var now = dispatcher.Now;
        var batch = await notifications.GetByKeyAsync(e.OwnerId, Notification.LikeGroupKey(e.ActivityId), cancellationToken);
        if (batch is null)
        {
            batch = Notification.ForLike(e.OwnerId, e.ActivityId, e.HikeName, e.LikerId, e.LikerName, now);
            notifications.Add(batch);
        }
        else if (!batch.AddLike(e.LikerId, e.LikerName, e.HikeName, now))
        {
            return;
        }

        await dispatcher.SaveAndPushAsync(batch.CanPush(now) ? [batch] : [], cancellationToken);
    }

    public async Task HikeCommentedAsync(HikeCommentedIntegrationEvent e, CancellationToken cancellationToken)
    {
        var added = await dispatcher.AddAsync(
            e.OwnerId,
            e.CommenterId,
            NotificationKind.HikeCommented,
            "New comment",
            $"{e.CommenterName} commented on {e.HikeName}: “{e.Excerpt}”",
            $"/feed/{e.ActivityId}",
            $"comment:{e.CommentId}",
            cancellationToken);
        await dispatcher.SaveAndPushAsync(added is null ? [] : [added], cancellationToken);
    }

    public async Task HikeSharedAsync(HikeSharedIntegrationEvent e, CancellationToken cancellationToken)
    {
        // Someone in several of the groups gets one item, naming the first of those groups.
        var groups = await directory.GetManyAsync(e.GroupIds, cancellationToken);
        var recipients = new Dictionary<string, string>();
        foreach (var group in groups)
        {
            foreach (var member in group.Members)
            {
                recipients.TryAdd(member.UserId, group.Name);
            }
        }

        var added = new List<Notification>();
        foreach (var (userId, groupName) in recipients)
        {
            if (await dispatcher.AddAsync(
                    userId,
                    e.AuthorId,
                    NotificationKind.HikeShared,
                    $"New hike in {groupName}",
                    $"{e.AuthorName} shared {e.HikeName}",
                    $"/feed/{e.ActivityId}",
                    $"share:{e.ActivityId}",
                    cancellationToken) is { } notification)
            {
                added.Add(notification);
            }
        }

        await dispatcher.SaveAndPushAsync(added, cancellationToken);
    }
}
