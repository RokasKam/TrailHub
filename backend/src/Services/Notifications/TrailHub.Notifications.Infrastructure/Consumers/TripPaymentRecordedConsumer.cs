using MassTransit;
using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Service;

namespace TrailHub.Notifications.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class TripPaymentRecordedConsumer(ITripNotificationService notifications) : IConsumer<TripPaymentRecordedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TripPaymentRecordedIntegrationEvent> context) => notifications.PaymentRecordedAsync(context.Message, context.CancellationToken);
}
