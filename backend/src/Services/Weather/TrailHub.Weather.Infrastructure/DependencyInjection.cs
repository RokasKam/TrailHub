using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.Weather.Application.Interfaces.Caching;
using TrailHub.Weather.Application.Interfaces.Providers;
using TrailHub.Weather.Infrastructure.Caching;
using TrailHub.Weather.Infrastructure.Providers.OpenMeteo;

namespace TrailHub.Weather.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWeatherInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(http =>
            {
                http.BaseAddress = new Uri(configuration["Weather:OpenMeteo:BaseUrl"] ?? "https://api.open-meteo.com/");
                http.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler();

        services.AddSingleton<IForecastCache, DistributedForecastCache>();
        if (configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "trailhub:weather:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }
}
