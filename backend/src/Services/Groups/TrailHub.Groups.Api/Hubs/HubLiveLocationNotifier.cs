using Microsoft.AspNetCore.SignalR;
using TrailHub.Groups.Application.Dtos;
using TrailHub.Groups.Application.Interfaces.Providers;

namespace TrailHub.Groups.Api.Hubs;

/// <summary>Broadcasts location changes to everyone viewing the group's live map, including the sender's other devices.</summary>
internal sealed class HubLiveLocationNotifier(IHubContext<LocationHub, ILocationClient> hub) : ILiveLocationNotifier
{
    public Task LocationUpdatedAsync(Guid groupId, LiveLocationDto location, CancellationToken cancellationToken) =>
        hub.Clients.Group(LocationHub.GroupName(groupId)).LocationUpdated(groupId, location);

    public Task LocationRemovedAsync(Guid groupId, string userId, CancellationToken cancellationToken) =>
        hub.Clients.Group(LocationHub.GroupName(groupId)).LocationRemoved(groupId, userId);
}
