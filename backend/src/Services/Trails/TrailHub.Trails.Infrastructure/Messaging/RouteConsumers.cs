using MassTransit;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Contracts;
using TrailHub.Trails.Application.Community;
using TrailHub.Trails.Application.Trails;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Messaging;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class RoutePublishedConsumer(ICommandHandler<PublishRouteAsTrailCommand> handler, ILogger<RoutePublishedConsumer> logger)
    : IConsumer<RoutePublishedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RoutePublishedIntegrationEvent> context)
    {
        var e = context.Message;
        var difficulty = Enum.TryParse<Difficulty>(e.Difficulty, ignoreCase: true, out var d) ? d : Difficulty.Moderate;
        var result = await handler.Handle(
            new PublishRouteAsTrailCommand(e.RouteId, e.OwnerId, e.Name, e.Description, e.Region, difficulty, e.Points, e.PublishedAtUtc),
            context.CancellationToken);

        // A rejected route (invalid data, someone else's trail id) won't succeed on retry; log it instead of redelivering forever.
        if (result.IsFailure)
        {
            logger.LogWarning("Couldn't publish route {RouteId} as a trail: {Error}", e.RouteId, result.Error!.Message);
        }
    }
}

internal sealed class RouteUnpublishedConsumer(ICommandHandler<UnpublishTrailCommand> handler) : IConsumer<RouteUnpublishedIntegrationEvent>
{
    public Task Consume(ConsumeContext<RouteUnpublishedIntegrationEvent> context) =>
        handler.Handle(new UnpublishTrailCommand(context.Message.RouteId, context.Message.OwnerId, context.Message.UnpublishedAtUtc), context.CancellationToken);
}
