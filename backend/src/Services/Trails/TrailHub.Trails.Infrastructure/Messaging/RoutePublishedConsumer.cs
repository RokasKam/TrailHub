using MassTransit;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Contracts;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Messaging;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class RoutePublishedConsumer(IPublishedRouteService publishedRoutes, ILogger<RoutePublishedConsumer> logger)
    : IConsumer<RoutePublishedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RoutePublishedIntegrationEvent> context)
    {
        var e = context.Message;
        var difficulty = Enum.TryParse<Difficulty>(e.Difficulty, ignoreCase: true, out var d) ? d : Difficulty.Moderate;
        try
        {
            await publishedRoutes.PublishAsync(
                new PublishRouteAsTrailRequest(e.RouteId, e.OwnerId, e.Name, e.Description, e.Region, difficulty, e.Points, e.PublishedAtUtc),
                context.CancellationToken);
        }
        catch (AppException ex)
        {
            // A rejected route (invalid data, someone else's trail id) won't succeed on retry; log it instead of redelivering forever.
            logger.LogWarning("Couldn't publish route {RouteId} as a trail: {Error}", e.RouteId, ex.Message);
        }
    }
}
