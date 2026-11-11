namespace TrailHub.BuildingBlocks.Application;

/// <summary>Publishes integration events to other services through the message bus.</summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class;
}
