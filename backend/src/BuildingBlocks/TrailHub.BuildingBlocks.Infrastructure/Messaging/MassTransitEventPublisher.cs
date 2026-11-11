using MassTransit;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.BuildingBlocks.Infrastructure.Messaging;

/// <summary>Publishes integration events on the MassTransit bus.</summary>
internal sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class =>
        publishEndpoint.Publish(integrationEvent, cancellationToken);
}
