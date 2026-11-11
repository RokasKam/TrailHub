using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Mappings;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class PointOfInterestService(ITrailRepository trails, IUnitOfWork unitOfWork, ICurrentUser user) : IPointOfInterestService
{
    public async Task<PointOfInterestResponse> AddAsync(Guid trailId, AddPointOfInterestRequest request, CancellationToken cancellationToken)
    {
        var location = GeoPoint.Create(request.Latitude, request.Longitude, request.Elevation).GetValueOrThrow();
        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        var added = trail.AddPointOfInterest(request.Name, request.Type, location, request.Description).GetValueOrThrow();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return added.ToResponse();
    }

    public async Task RemoveAsync(Guid trailId, Guid pointOfInterestId, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        trail.RemovePointOfInterest(pointOfInterestId, user.UserId).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
