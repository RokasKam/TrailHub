using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Application.Services;

internal sealed class TripNotificationService(NotificationDispatcher dispatcher) : ITripNotificationService
{
    public async Task ExpenseAddedAsync(TripExpenseAddedIntegrationEvent e, CancellationToken cancellationToken)
    {
        var added = new List<Notification>();
        foreach (var share in e.Shares.Where(s => s.Amount > 0).DistinctBy(s => s.UserId))
        {
            if (await dispatcher.AddAsync(
                    share.UserId,
                    e.CreatedById,
                    NotificationKind.TripExpenseAdded,
                    e.TripName,
                    $"{e.CreatedByName} added {e.Description} · your share {NotificationTexts.Money(share.Amount, e.Currency)}",
                    $"/trips/{e.TripId}/expenses",
                    $"expense:{e.ExpenseId}",
                    cancellationToken) is { } notification)
            {
                added.Add(notification);
            }
        }

        await dispatcher.SaveAndPushAsync(added, cancellationToken);
    }

    public async Task PaymentRecordedAsync(TripPaymentRecordedIntegrationEvent e, CancellationToken cancellationToken)
    {
        var amount = NotificationTexts.Money(e.Amount, e.Currency);
        var key = $"payment:{e.PaymentId}";
        var url = $"/trips/{e.TripId}/expenses";
        var toBody = $"{e.FromName} paid you {amount}";
        var fromBody = e.RecordedById == e.ToId
            ? $"{e.ToName} confirmed your payment of {amount}"
            : $"Your payment of {amount} to {e.ToName} was recorded";

        var added = new List<Notification>();
        foreach (var (recipient, body) in new[] { (e.ToId, toBody), (e.FromId, fromBody) })
        {
            if (await dispatcher.AddAsync(recipient, e.RecordedById, NotificationKind.TripPaymentRecorded, e.TripName, body, url, key, cancellationToken) is { } notification)
            {
                added.Add(notification);
            }
        }

        await dispatcher.SaveAndPushAsync(added, cancellationToken);
    }

    public async Task GearUnclaimedAsync(TripGearUnclaimedIntegrationEvent e, CancellationToken cancellationToken)
    {
        var added = new List<Notification>();
        foreach (var memberId in e.MemberIds.Distinct())
        {
            if (await dispatcher.AddAsync(
                    memberId,
                    null,
                    NotificationKind.TripGearUnclaimed,
                    e.TripName,
                    $"{NotificationTexts.Items(e.UnclaimedCount)} still unclaimed for {e.TripName}",
                    $"/trips/{e.TripId}",
                    $"gear:{e.TripId}",
                    cancellationToken) is { } notification)
            {
                added.Add(notification);
            }
        }

        await dispatcher.SaveAndPushAsync(added, cancellationToken);
    }
}
