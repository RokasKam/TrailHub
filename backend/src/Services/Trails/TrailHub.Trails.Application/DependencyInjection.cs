using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Trails.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTrailsApplication(this IServiceCollection services) =>
        services.AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);
}
