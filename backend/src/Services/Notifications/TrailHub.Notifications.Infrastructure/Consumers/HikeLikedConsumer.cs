using MassTransit;
using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Service;

namespace TrailHub.Notifications.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class HikeLikedConsumer(IFeedNotificationService notifications) : IConsumer<HikeLikedIntegrationEvent>
{
    public Task Consume(ConsumeContext<HikeLikedIntegrationEvent> context) => notifications.HikeLikedAsync(context.Message, context.CancellationToken);
}
