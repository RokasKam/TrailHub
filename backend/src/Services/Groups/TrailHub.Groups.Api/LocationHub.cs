using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Groups.Application.Groups;

namespace TrailHub.Groups.Api;

/// <summary>Server → client messages. Typed so the contract with the app lives in one place.</summary>
public interface ILocationClient
{
    Task LocationUpdated(Guid groupId, LiveLocationDto location);

    Task LocationRemoved(Guid groupId, string userId);
}

/// <summary>
/// Real-time location sharing over WebSockets (<c>/hubs/groups</c>). Clients call <see cref="JoinGroup"/>
/// for each group they're viewing, then <see cref="UpdateLocation"/> every few seconds while hiking.
/// All checks go through the application layer; the hub only fans messages out.
/// </summary>
[Authorize]
public sealed class LocationHub(
    IQueryHandler<GetLiveLocationsQuery, IReadOnlyList<LiveLocationDto>> getLocations,
    ICommandHandler<ShareLocationCommand, LiveLocationDto> shareLocation,
    ICommandHandler<StopSharingLocationCommand> stopSharing)
    : Hub<ILocationClient>
{
    public async Task<IReadOnlyList<LiveLocationDto>> JoinGroup(Guid groupId)
    {
        var snapshot = await getLocations.Handle(new GetLiveLocationsQuery(groupId), Context.ConnectionAborted);
        if (snapshot.IsFailure)
        {
            throw new HubException(snapshot.Error!.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId), Context.ConnectionAborted);
        return snapshot.Value;
    }

    public Task LeaveGroup(Guid groupId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(groupId));

    public async Task UpdateLocation(Guid groupId, double latitude, double longitude, double? elevation, double? accuracyMeters, double? headingDegrees)
    {
        // The handler broadcasts through ILiveLocationNotifier, same as REST updates.
        var result = await shareLocation.Handle(
            new ShareLocationCommand(groupId, latitude, longitude, elevation, accuracyMeters, headingDegrees),
            Context.ConnectionAborted);
        if (result.IsFailure)
        {
            throw new HubException(result.Error!.Message);
        }
    }

    public Task StopSharing(Guid groupId) => stopSharing.Handle(new StopSharingLocationCommand(groupId), Context.ConnectionAborted);

    internal static string GroupName(Guid groupId) => $"group:{groupId:N}";
}

/// <summary>Broadcasts location changes to everyone viewing the group's live map, including the sender's other devices.</summary>
internal sealed class HubLiveLocationNotifier(IHubContext<LocationHub, ILocationClient> hub) : ILiveLocationNotifier
{
    public Task LocationUpdatedAsync(Guid groupId, LiveLocationDto location, CancellationToken cancellationToken) =>
        hub.Clients.Group(LocationHub.GroupName(groupId)).LocationUpdated(groupId, location);

    public Task LocationRemovedAsync(Guid groupId, string userId, CancellationToken cancellationToken) =>
        hub.Clients.Group(LocationHub.GroupName(groupId)).LocationRemoved(groupId, userId);
}
