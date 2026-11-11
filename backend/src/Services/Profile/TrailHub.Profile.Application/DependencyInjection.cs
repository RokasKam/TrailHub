using Microsoft.Extensions.DependencyInjection;
using TrailHub.Profile.Application.Interfaces.Service;
using TrailHub.Profile.Application.Services;

namespace TrailHub.Profile.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProfileApplication(this IServiceCollection services) =>
        services
            .AddScoped<IProfileProvisioner, ProfileProvisioner>()
            .AddScoped<IProfileService, ProfileService>()
            .AddScoped<IProfileHikeService, ProfileHikeService>();
}
