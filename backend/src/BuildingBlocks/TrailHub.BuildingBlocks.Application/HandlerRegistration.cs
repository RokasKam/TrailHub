using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace TrailHub.BuildingBlocks.Application;

public static class HandlerRegistration
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

    /// <summary>
    /// Registers every non-generic command/query handler in <paramref name="assembly"/> as scoped.
    /// Generic handlers must be closed and registered by the service itself.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var handlers = assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });
        foreach (var implementation in handlers)
        {
            var interfaces = implementation.GetInterfaces()
                .Where(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition()));
            foreach (var serviceType in interfaces)
            {
                services.AddScoped(serviceType, implementation);
            }
        }

        return services;
    }
}
