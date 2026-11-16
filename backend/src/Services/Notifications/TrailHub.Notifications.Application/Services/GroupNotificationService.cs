using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Application.Services;

internal sealed class GroupNotificationService(NotificationDispatcher dispatcher, IGroupDirectory directory) : IGroupNotificationService
{
    public async Task GroupHikeScheduledAsync(GroupHikeScheduledIntegrationEvent e, CancellationToken cancellationToken)
    {
        var group = await directory.GetAsync(e.GroupId, cancellationToken);
        var groupName = e.GroupName ?? group?.Name ?? "your group";
        var scheduler = e.ScheduledById is { } id ? group?.NameOf(id) : null;
        var body = scheduler is null
            ? $"{e.Title} on {NotificationTexts.Day(e.ScheduledAtUtc)}"
            : $"{scheduler} scheduled {e.Title} for {NotificationTexts.Day(e.ScheduledAtUtc)}";

        var added = new List<Notification>();
        foreach (var memberId in e.MemberUserIds.Distinct())
        {
            if (await dispatcher.AddAsync(
                    memberId,
                    e.ScheduledById,
                    NotificationKind.GroupHikeScheduled,
                    $"Group hike in {groupName}",
                    body,
                    $"/groups/{e.GroupId}",
                    $"group-hike:{e.HikeId}",
                    cancellationToken) is { } notification)
            {
                added.Add(notification);
            }
        }

        await dispatcher.SaveAndPushAsync(added, cancellationToken);
    }
}
