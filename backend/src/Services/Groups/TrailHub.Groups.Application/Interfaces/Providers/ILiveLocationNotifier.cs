using TrailHub.Groups.Application.Dtos;

namespace TrailHub.Groups.Application.Interfaces.Providers;

/// <summary>
/// Pushes live-location changes to members watching the group's map (SignalR in the API layer).
/// Used for updates from both the WebSocket hub and the REST endpoint (background updates).
/// </summary>
public interface ILiveLocationNotifier
{
    Task LocationUpdatedAsync(Guid groupId, LiveLocationDto location, CancellationToken cancellationToken);

    Task LocationRemovedAsync(Guid groupId, string userId, CancellationToken cancellationToken);
}
