using Microsoft.AspNetCore.SignalR;
using TrailHub.Trips.Application.Interfaces.Providers;

namespace TrailHub.Trips.Api;

internal sealed class HubTripChangeNotifier(IHubContext<TripHub, ITripClient> hub) : ITripChangeNotifier
{
    public Task TripChangedAsync(Guid tripId, CancellationToken cancellationToken) =>
        hub.Clients.Group(TripHub.GroupName(tripId)).TripChanged(tripId);
}
