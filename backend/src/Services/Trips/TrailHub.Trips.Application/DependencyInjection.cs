using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Trips.Application.Trips;

namespace TrailHub.Trips.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTripsApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddHandlersFromAssembly(assembly);

        // One generic handler serves every TripChange command.
        foreach (var command in assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(TripChange))))
        {
            services.AddScoped(
                typeof(ICommandHandler<,>).MakeGenericType(command, typeof(TripDto)),
                typeof(TripChangeHandler<>).MakeGenericType(command));
        }

        return services;
    }
}
