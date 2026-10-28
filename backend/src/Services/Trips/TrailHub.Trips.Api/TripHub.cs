using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Trips.Application.Trips;

namespace TrailHub.Trips.Api;

/// <summary>Server → client messages.</summary>
public interface ITripClient
{
    /// <summary>Something on the trip changed; reload it over REST. Carries no data, so a stale subscription leaks nothing.</summary>
    Task TripChanged(Guid tripId);
}

/// <summary>
/// Live updates for open trips (<c>/hubs/trips</c>). The app calls <see cref="WatchTrip"/> while a trip screen is open,
/// and reloads the trip whenever <see cref="ITripClient.TripChanged"/> arrives (someone claimed or ticked an item).
/// </summary>
[Authorize]
public sealed class TripHub(IQueryHandler<GetTripQuery, TripDto> getTrip) : Hub<ITripClient>
{
    public async Task WatchTrip(Guid tripId)
    {
        var trip = await getTrip.Handle(new GetTripQuery(tripId), Context.ConnectionAborted);
        if (trip.IsFailure)
        {
            throw new HubException(trip.Error!.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tripId), Context.ConnectionAborted);
    }

    public Task UnwatchTrip(Guid tripId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tripId));

    internal static string GroupName(Guid tripId) => $"trip:{tripId:N}";
}

internal sealed class HubTripChangeNotifier(IHubContext<TripHub, ITripClient> hub) : ITripChangeNotifier
{
    public Task TripChangedAsync(Guid tripId, CancellationToken cancellationToken) =>
        hub.Clients.Group(TripHub.GroupName(tripId)).TripChanged(tripId);
}
