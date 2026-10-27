using Microsoft.Extensions.DependencyInjection;
using TrailHub.Activity.Application.Activities;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Activity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddActivityApplication(this IServiceCollection services) =>
        services
            .AddScoped<ActivitySaver>()
            .AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);
}
