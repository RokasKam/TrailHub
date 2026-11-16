using TrailHub.BuildingBlocks.Application;
using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Application.Models;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Application.Services;

/// <summary>
/// Every event is handled the same way: add inbox items (never for the person who did it, never twice for one event),
/// save, then push the ones whose category the recipient has switched on to all their devices.
/// </summary>
internal sealed class NotificationDispatcher(
    INotificationRepository notifications,
    INotificationPreferencesRepository preferences,
    IPushDeviceRepository devices,
    IPushSender sender,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Adds an inbox item for <paramref name="recipientId"/>, unless they are <paramref name="actorId"/> or already have
    /// an item with <paramref name="groupKey"/>. Returns the new item, or null when there is none.
    /// </summary>
    public async Task<Notification?> AddAsync(
        string recipientId,
        string? actorId,
        NotificationKind kind,
        string title,
        string body,
        string targetUrl,
        string? groupKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipientId) || recipientId == actorId)
        {
            return null;
        }

        if (groupKey is not null && await notifications.GetByKeyAsync(recipientId, groupKey, cancellationToken) is not null)
        {
            return null;
        }

        var notification = Notification.Create(recipientId, kind, title, body, targetUrl, actorId, groupKey, Now);
        notifications.Add(notification);
        return notification;
    }

    /// <summary>Saves everything pending, then pushes <paramref name="toPush"/>. Push problems never fail the caller.</summary>
    public async Task SaveAndPushAsync(IReadOnlyCollection<Notification> toPush, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (toPush.Count == 0)
        {
            return;
        }

        var now = Now;
        var messages = new List<PushMessage>();
        foreach (var forUser in toPush.GroupBy(n => n.UserId))
        {
            var prefs = await preferences.GetAsync(forUser.Key, cancellationToken);
            var allowed = forUser.Where(n => prefs?.AllowsPush(n.Category) ?? true).ToList();
            if (allowed.Count == 0)
            {
                continue;
            }

            var userDevices = await devices.GetForUserAsync(forUser.Key, cancellationToken);
            if (userDevices.Count == 0)
            {
                continue;
            }

            var badge = await notifications.CountUnreadAsync(forUser.Key, cancellationToken);
            foreach (var notification in allowed)
            {
                notification.MarkPushed(now);
                messages.AddRange(userDevices.Select(d => new PushMessage(d.Token, notification.Title, notification.Body, notification.TargetUrl, notification.Id, badge)));
            }
        }

        if (messages.Count == 0)
        {
            return;
        }

        var unregistered = await sender.SendAsync(messages, cancellationToken);
        if (unregistered.Count > 0)
        {
            await devices.RemoveTokensAsync(unregistered, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
