using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;

namespace TrailHub.Trips.Application.Interfaces.Service;

/// <summary>The caller's reusable gear lists and the built-in starter lists.</summary>
public interface IGearListService
{
    /// <summary>Your lists, most recently changed first, then the starter lists.</summary>
    Task<IReadOnlyList<GearListResponse>> GetListsAsync(CancellationToken cancellationToken);

    /// <summary>Creates a list.</summary>
    Task<GearListResponse> CreateAsync(GearListRequest request, CancellationToken cancellationToken);

    /// <summary>Replaces one of your lists.</summary>
    Task<GearListResponse> UpdateAsync(Guid id, GearListRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
