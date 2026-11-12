using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;

namespace TrailHub.Trips.Application.Interfaces.Service;

/// <summary>
/// Trips and their day plans. A trip is visible to its owner and, once shared, to the members of its group; anything
/// else is reported as not found. The plan (dates, days, trails, route, status) is the owner's to change.
/// Every edit tells people watching the trip that it changed and returns the updated trip.
/// </summary>
public interface ITripService
{
    /// <summary>The caller's own trips and those shared with their groups; with <paramref name="groupId"/>, only that group's trips.</summary>
    Task<IReadOnlyList<TripSummaryResponse>> GetTripsAsync(Guid? groupId, CancellationToken cancellationToken);

    Task<TripResponse> GetTripAsync(Guid id, CancellationToken cancellationToken);

    Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Shares the trip with one of the owner's groups, or makes it private again (<c>GroupId = null</c>).</summary>
    Task<TripResponse> ShareAsync(Guid id, TripGroupRequest request, CancellationToken cancellationToken);

    /// <summary>Sets the currency expenses are kept in. Owner only, and only while no expenses or payments are logged.</summary>
    Task<TripResponse> SetCurrencyAsync(Guid id, TripCurrencyRequest request, CancellationToken cancellationToken);

    /// <summary>Attaches a planned route to the trip (or detaches it with null). The route's days line up with the trip's days.</summary>
    Task<TripResponse> AttachRouteAsync(Guid id, TripRouteRequest request, CancellationToken cancellationToken);

    Task<TripResponse> UpdateDetailsAsync(Guid id, TripDetailsRequest request, CancellationToken cancellationToken);

    Task<TripResponse> RescheduleAsync(Guid id, TripDatesRequest request, CancellationToken cancellationToken);

    Task<TripResponse> UpdateDayAsync(Guid id, int dayNumber, TripDayRequest request, CancellationToken cancellationToken);

    Task<TripResponse> AddTrailAsync(Guid id, int dayNumber, Guid trailId, CancellationToken cancellationToken);

    Task<TripResponse> RemoveTrailAsync(Guid id, int dayNumber, Guid trailId, CancellationToken cancellationToken);

    Task<TripResponse> CompleteAsync(Guid id, CancellationToken cancellationToken);

    Task<TripResponse> CancelAsync(Guid id, CancellationToken cancellationToken);
}
