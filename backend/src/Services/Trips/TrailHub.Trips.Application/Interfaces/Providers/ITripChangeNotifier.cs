namespace TrailHub.Trips.Application.Interfaces.Providers;

/// <summary>Tells people looking at a trip that it changed, so their app reloads it.</summary>
public interface ITripChangeNotifier
{
    Task TripChangedAsync(Guid tripId, CancellationToken cancellationToken);
}
