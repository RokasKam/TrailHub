using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trips.Application.Interfaces.Service;

namespace TrailHub.Trips.Api;

/// <summary>
/// Live updates for open trips (<c>/hubs/trips</c>). The app calls <see cref="WatchTrip"/> while a trip screen is open,
/// and reloads the trip whenever <see cref="ITripClient.TripChanged"/> arrives (someone claimed or ticked an item).
/// </summary>
[Authorize]
public sealed class TripHub(ITripService tripService) : Hub<ITripClient>
{
    public async Task WatchTrip(Guid tripId)
    {
        try
        {
            // Only people who can see the trip may watch it.
            await tripService.GetTripAsync(tripId, Context.ConnectionAborted);
        }
        catch (AppException e)
        {
            throw new HubException(e.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tripId), Context.ConnectionAborted);
    }

    public Task UnwatchTrip(Guid tripId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tripId));

    internal static string GroupName(Guid tripId) => $"trip:{tripId:N}";
}
