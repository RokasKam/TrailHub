using TrailHub.Contracts;

namespace TrailHub.Notifications.Application.Interfaces.Service;

/// <summary>Turns trip events (expenses, payments, gear reminders) into notifications.</summary>
public interface ITripNotificationService
{
    Task ExpenseAddedAsync(TripExpenseAddedIntegrationEvent e, CancellationToken cancellationToken);

    Task PaymentRecordedAsync(TripPaymentRecordedIntegrationEvent e, CancellationToken cancellationToken);

    Task GearUnclaimedAsync(TripGearUnclaimedIntegrationEvent e, CancellationToken cancellationToken);
}
