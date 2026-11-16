using MassTransit;
using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Service;

namespace TrailHub.Notifications.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class GroupHikeScheduledConsumer(IGroupNotificationService notifications) : IConsumer<GroupHikeScheduledIntegrationEvent>
{
    public Task Consume(ConsumeContext<GroupHikeScheduledIntegrationEvent> context) => notifications.GroupHikeScheduledAsync(context.Message, context.CancellationToken);
}
