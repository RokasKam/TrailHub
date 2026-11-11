using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;

namespace TrailHub.Trips.Application.Interfaces.Service;

/// <summary>A trip's packing checklist. Open to everyone the trip is shared with, not only the owner.</summary>
public interface ITripChecklistService
{
    Task<TripResponse> AddItemAsync(Guid tripId, ChecklistItemRequest request, CancellationToken cancellationToken);

    /// <summary>Adds several items to a trip's checklist; duplicates are skipped. Anyone who can use the checklist may do it.</summary>
    Task<TripResponse> AddItemsAsync(Guid tripId, ChecklistItemsRequest request, CancellationToken cancellationToken);

    Task<TripResponse> SetItemDoneAsync(Guid tripId, Guid itemId, ChecklistDoneRequest request, CancellationToken cancellationToken);

    Task<TripResponse> ClaimItemAsync(Guid tripId, Guid itemId, ChecklistClaimRequest request, CancellationToken cancellationToken);

    Task<TripResponse> RemoveItemAsync(Guid tripId, Guid itemId, CancellationToken cancellationToken);

    /// <summary>Copies a gear list (yours or a starter list) into a trip's checklist.</summary>
    Task<TripResponse> AddGearListAsync(Guid tripId, string listId, CancellationToken cancellationToken);

    /// <summary>Saves a trip's checklist as one of your gear lists, keeping each item's personal/group kind.</summary>
    Task<GearListResponse> SaveAsGearListAsync(Guid tripId, SaveAsGearListRequest request, CancellationToken cancellationToken);
}
