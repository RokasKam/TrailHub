using MassTransit;
using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Service;

namespace TrailHub.Notifications.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class HikeSharedConsumer(IFeedNotificationService notifications) : IConsumer<HikeSharedIntegrationEvent>
{
    public Task Consume(ConsumeContext<HikeSharedIntegrationEvent> context) => notifications.HikeSharedAsync(context.Message, context.CancellationToken);
}
