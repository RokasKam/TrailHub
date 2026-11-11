using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>Any signed-in hiker can add a POI (e.g. a spring they found); only the owner can remove one.</summary>
public interface IPointOfInterestService
{
    Task<PointOfInterestResponse> AddAsync(Guid trailId, AddPointOfInterestRequest request, CancellationToken cancellationToken);

    Task RemoveAsync(Guid trailId, Guid pointOfInterestId, CancellationToken cancellationToken);
}
