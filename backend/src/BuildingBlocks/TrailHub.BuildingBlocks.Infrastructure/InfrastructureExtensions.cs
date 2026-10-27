using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Infrastructure.Gpx;

namespace TrailHub.BuildingBlocks.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddGpxSupport(this IServiceCollection services) =>
        services.AddSingleton<IGpxSerializer, GpxSerializer>();
}
