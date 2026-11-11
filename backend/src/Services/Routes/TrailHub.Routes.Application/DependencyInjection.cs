using Microsoft.Extensions.DependencyInjection;
using TrailHub.Routes.Application.Interfaces.Service;
using TrailHub.Routes.Application.Services;

namespace TrailHub.Routes.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRoutesApplication(this IServiceCollection services) =>
        services
            .AddScoped<IRouteService, RouteService>()
            .AddScoped<IRoutePlanningService, RoutePlanningService>()
            .AddScoped<IRouteSharingService, RouteSharingService>()
            .AddScoped<IPlaceService, PlaceService>()
            .AddScoped<IGroupMembershipService, GroupMembershipService>();
}
