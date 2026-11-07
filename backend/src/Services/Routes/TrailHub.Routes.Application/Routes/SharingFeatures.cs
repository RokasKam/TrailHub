using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Contracts;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Routes;

/// <summary>Shares a route with one of the owner's groups (<c>Share = true</c>) or stops sharing it.</summary>
public sealed record ShareRouteCommand(Guid RouteId, Guid GroupId, bool Share) : ICommand<RouteDto>;

internal sealed class ShareRouteHandler(IRouteRepository routes, IGroupDirectory directory, IUnitOfWork unitOfWork, ICurrentUser user)
    : ICommandHandler<ShareRouteCommand, RouteDto>
{
    public async Task<Result<RouteDto>> Handle(ShareRouteCommand command, CancellationToken cancellationToken)
    {
        if (await routes.LoadVisibleAsync(directory, command.RouteId, user.UserId, cancellationToken) is not (PlannedRoute route, _))
        {
            return RouteErrors.NotFound;
        }

        if (route.OwnerId != user.UserId)
        {
            return RouteErrors.NotOwner;
        }

        if (command.Share)
        {
            var group = await directory.GetAsync(command.GroupId, cancellationToken);
            if (group is null || !group.IsMember(user.UserId))
            {
                return RouteErrors.GroupNotFound;
            }

            route.ShareWithGroup(command.GroupId);
        }
        else
        {
            route.UnshareGroup(command.GroupId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await routes.LoadVisibleAsync(directory, route.Id, user.UserId, cancellationToken) is (PlannedRoute updated, var groups)
            ? updated.ToDto(user.UserId, groups)
            : RouteErrors.NotFound;
    }
}

/// <summary>Copies a route someone shared with you into your own routes, so you can change it.</summary>
public sealed record CopyRouteCommand(Guid RouteId) : ICommand<RouteDto>;

internal sealed class CopyRouteHandler(IRouteRepository routes, IGroupDirectory directory, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CopyRouteCommand, RouteDto>
{
    public async Task<Result<RouteDto>> Handle(CopyRouteCommand command, CancellationToken cancellationToken)
    {
        if (await routes.LoadVisibleAsync(directory, command.RouteId, user.UserId, cancellationToken) is not (PlannedRoute route, _))
        {
            return RouteErrors.NotFound;
        }

        var copy = route.CopyFor(user.UserId, clock.GetUtcNow().UtcDateTime);
        routes.Add(copy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return copy.ToDto(user.UserId, []);
    }
}

/// <summary>
/// Publishes the route to Discover as a public trail (same id as the route), or updates that trail when published
/// again. The trail is a copy: later edits to the route don't change it until the route is published again.
/// </summary>
public sealed record PublishRouteCommand(Guid RouteId, string Name, string? Description, string Region, PlanDifficulty Difficulty) : ICommand<RouteDto>;

internal sealed class PublishRouteHandler(IRouteRepository routes, IGroupDirectory directory, IUnitOfWork unitOfWork, IEventPublisher events, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<PublishRouteCommand, RouteDto>
{
    public async Task<Result<RouteDto>> Handle(PublishRouteCommand command, CancellationToken cancellationToken)
    {
        if (await routes.LoadVisibleAsync(directory, command.RouteId, user.UserId, cancellationToken) is not (PlannedRoute route, var groups))
        {
            return RouteErrors.NotFound;
        }

        if (route.OwnerId != user.UserId)
        {
            return RouteErrors.NotOwner;
        }

        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > PlannedRoute.MaxNameLength
            || string.IsNullOrWhiteSpace(command.Region) || command.Region.Length > PlannedRoute.MaxNameLength
            || command.Description?.Length > PlannedRoute.MaxTextLength)
        {
            return RouteErrors.InvalidPublishDetails;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (route.MarkPublished(now) is { IsFailure: true } failed)
        {
            return failed.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishAsync(
            new RoutePublishedIntegrationEvent(
                route.Id,
                route.OwnerId,
                command.Name.Trim(),
                string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
                command.Region.Trim(),
                command.Difficulty.ToString(),
                route.Points.Select(p => new[] { p.Latitude, p.Longitude, p.Elevation }).ToList(),
                now),
            cancellationToken);
        return route.ToDto(user.UserId, groups);
    }
}

/// <summary>Takes the route's trail out of Discover. Publishing again brings it back (unless moderators hid it).</summary>
public sealed record UnpublishRouteCommand(Guid RouteId) : ICommand<RouteDto>;

internal sealed class UnpublishRouteHandler(IRouteRepository routes, IGroupDirectory directory, IUnitOfWork unitOfWork, IEventPublisher events, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<UnpublishRouteCommand, RouteDto>
{
    public async Task<Result<RouteDto>> Handle(UnpublishRouteCommand command, CancellationToken cancellationToken)
    {
        if (await routes.LoadVisibleAsync(directory, command.RouteId, user.UserId, cancellationToken) is not (PlannedRoute route, var groups))
        {
            return RouteErrors.NotFound;
        }

        if (route.OwnerId != user.UserId)
        {
            return RouteErrors.NotOwner;
        }

        route.MarkUnpublished();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishAsync(new RouteUnpublishedIntegrationEvent(route.Id, route.OwnerId, clock.GetUtcNow().UtcDateTime), cancellationToken);
        return route.ToDto(user.UserId, groups);
    }
}
