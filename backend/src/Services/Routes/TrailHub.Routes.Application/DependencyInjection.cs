using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Routes.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRoutesApplication(this IServiceCollection services) =>
        services.AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);
}
