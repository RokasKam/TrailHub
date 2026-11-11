using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Application.Interfaces.Providers;

public interface IWeatherProvider
{
    Task<Forecast> GetForecastAsync(double latitude, double longitude, double? elevationMeters, int days, CancellationToken cancellationToken);
}
