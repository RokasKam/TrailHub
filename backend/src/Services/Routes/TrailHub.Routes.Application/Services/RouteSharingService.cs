using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Contracts;
using TrailHub.Routes.Application.Interfaces.Repository;
using TrailHub.Routes.Application.Interfaces.Service;
using TrailHub.Routes.Application.Mappings;
using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Services;

internal sealed class RouteSharingService(
    IRouteRepository routes,
    IGroupDirectory directory,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    ICurrentUser user,
    TimeProvider clock) : IRouteSharingService
{
    public async Task<RouteResponse> ShareAsync(Guid id, Guid groupId, bool share, CancellationToken cancellationToken)
    {
        var (route, _) = await routes.GetVisibleAsync(directory, id, user.UserId, cancellationToken);
        EnsureOwner(route);

        if (share)
        {
            var group = await directory.GetAsync(groupId, cancellationToken);
            if (group is null || !group.IsMember(user.UserId))
            {
                throw RouteErrors.GroupNotFound.ToException();
            }

            route.ShareWithGroup(groupId);
        }
        else
        {
            route.UnshareGroup(groupId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var (updated, groups) = await routes.GetVisibleAsync(directory, route.Id, user.UserId, cancellationToken);
        return updated.ToResponse(user.UserId, groups);
    }

    public async Task<RouteResponse> CopyAsync(Guid id, CancellationToken cancellationToken)
    {
        var (route, _) = await routes.GetVisibleAsync(directory, id, user.UserId, cancellationToken);
        var copy = route.CopyFor(user.UserId, clock.GetUtcNow().UtcDateTime);
        routes.Add(copy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return copy.ToResponse(user.UserId, []);
    }

    public async Task<RouteResponse> PublishAsync(Guid id, PublishRouteRequest request, CancellationToken cancellationToken)
    {
        var (route, groups) = await routes.GetVisibleAsync(directory, id, user.UserId, cancellationToken);
        EnsureOwner(route);

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > PlannedRoute.MaxNameLength
            || string.IsNullOrWhiteSpace(request.Region) || request.Region.Length > PlannedRoute.MaxNameLength
            || request.Description?.Length > PlannedRoute.MaxTextLength)
        {
            throw RouteErrors.InvalidPublishDetails.ToException();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        route.MarkPublished(now).ThrowIfFailure();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishAsync(
            new RoutePublishedIntegrationEvent(
                route.Id,
                route.OwnerId,
                request.Name.Trim(),
                string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                request.Region.Trim(),
                request.Difficulty.ToString(),
                route.Points.Select(p => new[] { p.Latitude, p.Longitude, p.Elevation }).ToList(),
                now),
            cancellationToken);
        return route.ToResponse(user.UserId, groups);
    }

    public async Task<RouteResponse> UnpublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var (route, groups) = await routes.GetVisibleAsync(directory, id, user.UserId, cancellationToken);
        EnsureOwner(route);

        route.MarkUnpublished();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishAsync(new RouteUnpublishedIntegrationEvent(route.Id, route.OwnerId, clock.GetUtcNow().UtcDateTime), cancellationToken);
        return route.ToResponse(user.UserId, groups);
    }

    /// <summary>Group members can see a shared route, but only its author can change, share or publish it.</summary>
    private void EnsureOwner(PlannedRoute route)
    {
        if (route.OwnerId != user.UserId)
        {
            throw RouteErrors.NotOwner.ToException();
        }
    }
}
