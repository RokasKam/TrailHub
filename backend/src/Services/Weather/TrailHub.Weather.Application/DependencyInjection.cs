using Microsoft.Extensions.DependencyInjection;
using TrailHub.Weather.Application.Interfaces.Service;
using TrailHub.Weather.Application.Services;

namespace TrailHub.Weather.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWeatherApplication(this IServiceCollection services) =>
        services.AddScoped<IWeatherService, WeatherService>();
}
