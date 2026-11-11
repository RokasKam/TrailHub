using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class PublishedRouteService(ITrailRepository trails, IUnitOfWork unitOfWork) : IPublishedRouteService
{
    public async Task PublishAsync(PublishRouteAsTrailRequest request, CancellationToken cancellationToken)
    {
        var route = new List<GeoPoint>(request.Points.Count);
        foreach (var p in request.Points)
        {
            if (p.Length < 2 || p[0] is not { } lat || p[1] is not { } lon || GeoPoint.Create(lat, lon, p.Length > 2 ? p[2] : null) is not { IsSuccess: true } point)
            {
                throw TrailErrors.RouteTooShort.ToException();
            }

            route.Add(point.Value);
        }

        if (await trails.GetByIdAsync(request.RouteId, cancellationToken) is { } existing)
        {
            existing.UpdateDetails(request.Name, request.Description, request.Region, request.Difficulty, request.OwnerId).ThrowIfFailure();
            existing.ReplaceRoute(route, request.OwnerId).ThrowIfFailure();

            // Publishing again undoes the author's own unpublish; a trail hidden after reports stays hidden.
            existing.Unhide(HiddenBy.Author);
        }
        else
        {
            var created = Trail.Create(request.Name, request.Description, request.Region, request.Difficulty, route, request.OwnerId, request.PublishedAtUtc, request.RouteId).GetValueOrThrow();
            trails.Add(created);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnpublishAsync(Guid trailId, string ownerId, DateTime unpublishedAtUtc, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(trailId, cancellationToken);
        if (trail is null || trail.CreatedBy != ownerId)
        {
            return;
        }

        trail.Hide(HiddenBy.Author, unpublishedAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
