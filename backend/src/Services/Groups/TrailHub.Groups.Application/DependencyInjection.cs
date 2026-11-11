using Microsoft.Extensions.DependencyInjection;
using TrailHub.Groups.Application.Interfaces.Service;
using TrailHub.Groups.Application.Services;

namespace TrailHub.Groups.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddGroupsApplication(this IServiceCollection services) =>
        services
            .AddScoped<IGroupService, GroupService>()
            .AddScoped<IGroupHikeService, GroupHikeService>()
            .AddScoped<ILiveLocationService, LiveLocationService>();
}
