namespace TrailHub.Trips.Api;

/// <summary>Server → client messages.</summary>
public interface ITripClient
{
    /// <summary>Something on the trip changed; reload it over REST. Carries no data, so a stale subscription leaks nothing.</summary>
    Task TripChanged(Guid tripId);
}
