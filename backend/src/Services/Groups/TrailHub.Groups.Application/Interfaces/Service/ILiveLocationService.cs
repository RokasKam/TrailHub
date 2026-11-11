using TrailHub.Groups.Application.Dtos;
using TrailHub.Groups.Application.Requests;

namespace TrailHub.Groups.Application.Interfaces.Service;

/// <summary>Live member positions, shared over REST or the SignalR hub and broadcast to everyone viewing the group's map.</summary>
public interface ILiveLocationService
{
    /// <summary>Stores the caller's position and broadcasts it to the group (members only).</summary>
    Task<LiveLocationDto> ShareAsync(Guid groupId, ShareLocationRequest request, CancellationToken cancellationToken);

    /// <summary>Stops sharing: removes the caller from the group's live map.</summary>
    Task StopSharingAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>Latest shared position of each member. Groups the caller isn't in are reported as not found.</summary>
    Task<IReadOnlyList<LiveLocationDto>> GetLocationsAsync(Guid groupId, CancellationToken cancellationToken);
}
