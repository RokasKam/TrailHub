using TrailHub.Trails.Application.Requests;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>Trails that come from routes published or unpublished in the Routes service.</summary>
public interface IPublishedRouteService
{
    /// <summary>
    /// A planned route its author published from the Routes service. Creates a public trail with the route's id, or,
    /// when the route is published again, updates that trail's details and line (reviews, photos and favourites stay).
    /// </summary>
    Task PublishAsync(PublishRouteAsTrailRequest request, CancellationToken cancellationToken);

    /// <summary>The author took the route the trail came from out of Discover (Routes service). Unknown trails are ignored.</summary>
    Task UnpublishAsync(Guid trailId, string ownerId, DateTime unpublishedAtUtc, CancellationToken cancellationToken);
}
