using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Profile.Application.Profiles;

namespace TrailHub.Profile.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProfileApplication(this IServiceCollection services) =>
        services
            .AddScoped<ProfileProvisioner>()
            .AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);
}
