using MassTransit;
using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Service;

namespace TrailHub.Notifications.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class HikeCommentedConsumer(IFeedNotificationService notifications) : IConsumer<HikeCommentedIntegrationEvent>
{
    public Task Consume(ConsumeContext<HikeCommentedIntegrationEvent> context) => notifications.HikeCommentedAsync(context.Message, context.CancellationToken);
}
