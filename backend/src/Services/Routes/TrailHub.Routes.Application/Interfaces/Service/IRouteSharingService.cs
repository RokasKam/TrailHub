using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;

namespace TrailHub.Routes.Application.Interfaces.Service;

/// <summary>Sharing routes with groups, copying shared routes, and publishing routes to Discover.</summary>
public interface IRouteSharingService
{
    /// <summary>Shares a route with one of the owner's groups (<paramref name="share"/> true) or stops sharing it.</summary>
    Task<RouteResponse> ShareAsync(Guid id, Guid groupId, bool share, CancellationToken cancellationToken);

    /// <summary>Copies a route someone shared with you into your own routes, so you can change it.</summary>
    Task<RouteResponse> CopyAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes the route to Discover as a public trail (same id as the route), or updates that trail when published
    /// again. The trail is a copy: later edits to the route don't change it until the route is published again.
    /// </summary>
    Task<RouteResponse> PublishAsync(Guid id, PublishRouteRequest request, CancellationToken cancellationToken);

    /// <summary>Takes the route's trail out of Discover. Publishing again brings it back (unless moderators hid it).</summary>
    Task<RouteResponse> UnpublishAsync(Guid id, CancellationToken cancellationToken);
}
