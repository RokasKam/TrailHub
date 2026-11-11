using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Routes.Application.Interfaces.Repository;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Services;

internal static class RouteAccess
{
    /// <summary>
    /// A route the user may see: their own, or one shared with a group they're in. Anything else is reported as
    /// missing so ids can't be probed. Also returns the groups it's shared with (that still exist), for the response.
    /// </summary>
    public static async Task<(PlannedRoute Route, IReadOnlyList<GroupInfo> SharedWith)?> LoadVisibleAsync(
        this IRouteRepository routes, IGroupDirectory directory, Guid routeId, string userId, CancellationToken cancellationToken)
    {
        if (await routes.GetByIdAsync(routeId, cancellationToken) is not { } route)
        {
            return null;
        }

        var groups = new List<GroupInfo>();
        foreach (var groupId in route.SharedWithGroups)
        {
            if (await directory.GetAsync(groupId, cancellationToken) is { } group)
            {
                groups.Add(group);
            }
        }

        return route.OwnerId == userId || groups.Any(g => g.IsMember(userId)) ? (route, groups) : null;
    }

    /// <summary>Like <see cref="LoadVisibleAsync"/>, but throws <see cref="RouteErrors.NotFound"/> when the user can't see it.</summary>
    public static async Task<(PlannedRoute Route, IReadOnlyList<GroupInfo> SharedWith)> GetVisibleAsync(
        this IRouteRepository routes, IGroupDirectory directory, Guid routeId, string userId, CancellationToken cancellationToken) =>
        await routes.LoadVisibleAsync(directory, routeId, userId, cancellationToken) ?? throw RouteErrors.NotFound.ToException();
}
