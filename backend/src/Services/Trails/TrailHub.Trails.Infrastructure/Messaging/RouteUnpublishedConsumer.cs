using MassTransit;
using TrailHub.Contracts;
using TrailHub.Trails.Application.Interfaces.Service;

namespace TrailHub.Trails.Infrastructure.Messaging;

/// <summary>Hides the trail a route became when its author unpublishes the route.</summary>
internal sealed class RouteUnpublishedConsumer(IPublishedRouteService publishedRoutes) : IConsumer<RouteUnpublishedIntegrationEvent>
{
    public Task Consume(ConsumeContext<RouteUnpublishedIntegrationEvent> context) =>
        publishedRoutes.UnpublishAsync(context.Message.RouteId, context.Message.OwnerId, context.Message.UnpublishedAtUtc, context.CancellationToken);
}
