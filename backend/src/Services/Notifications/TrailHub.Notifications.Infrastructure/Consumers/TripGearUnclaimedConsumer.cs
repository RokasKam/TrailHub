using MassTransit;
using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Service;

namespace TrailHub.Notifications.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class TripGearUnclaimedConsumer(ITripNotificationService notifications) : IConsumer<TripGearUnclaimedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TripGearUnclaimedIntegrationEvent> context) => notifications.GearUnclaimedAsync(context.Message, context.CancellationToken);
}
