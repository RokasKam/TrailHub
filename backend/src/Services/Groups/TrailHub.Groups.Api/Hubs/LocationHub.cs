using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Groups.Application.Dtos;
using TrailHub.Groups.Application.Interfaces.Service;
using TrailHub.Groups.Application.Requests;

namespace TrailHub.Groups.Api.Hubs;

/// <summary>
/// Real-time location sharing over WebSockets (<c>/hubs/groups</c>). Clients call <see cref="JoinGroup"/>
/// for each group they're viewing, then <see cref="UpdateLocation"/> every few seconds while hiking.
/// All checks go through the application layer; the hub only fans messages out.
/// </summary>
[Authorize]
public sealed class LocationHub(ILiveLocationService liveLocationService) : Hub<ILocationClient>
{
    public async Task<IReadOnlyList<LiveLocationDto>> JoinGroup(Guid groupId)
    {
        IReadOnlyList<LiveLocationDto> snapshot;
        try
        {
            snapshot = await liveLocationService.GetLocationsAsync(groupId, Context.ConnectionAborted);
        }
        catch (AppException e)
        {
            throw new HubException(e.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId), Context.ConnectionAborted);
        return snapshot;
    }

    public Task LeaveGroup(Guid groupId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(groupId));

    public async Task UpdateLocation(Guid groupId, double latitude, double longitude, double? elevation, double? accuracyMeters, double? headingDegrees)
    {
        // The service broadcasts through ILiveLocationNotifier, same as REST updates.
        try
        {
            await liveLocationService.ShareAsync(
                groupId,
                new ShareLocationRequest(latitude, longitude, elevation, accuracyMeters, headingDegrees),
                Context.ConnectionAborted);
        }
        catch (AppException e)
        {
            throw new HubException(e.Message);
        }
    }

    public Task StopSharing(Guid groupId) => liveLocationService.StopSharingAsync(groupId, Context.ConnectionAborted);

    internal static string GroupName(Guid groupId) => $"group:{groupId:N}";
}
