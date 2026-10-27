using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Groups.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddGroupsApplication(this IServiceCollection services) =>
        services.AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);
}
