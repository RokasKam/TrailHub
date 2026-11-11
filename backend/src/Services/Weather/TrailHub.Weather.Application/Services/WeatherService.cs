using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Weather.Application.Interfaces.Caching;
using TrailHub.Weather.Application.Interfaces.Providers;
using TrailHub.Weather.Application.Interfaces.Service;
using TrailHub.Weather.Application.Requests;
using TrailHub.Weather.Application.Responses;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Application.Services;

internal sealed class WeatherService(IWeatherProvider provider, IForecastCache cache) : IWeatherService
{
    public const int MaxForecastDays = 16;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(20);

    public async Task<ForecastResponse> GetForecastAsync(ForecastRequest request, CancellationToken cancellationToken)
    {
        if (GeoPoint.Create(request.Latitude, request.Longitude).Error is { } invalid)
        {
            throw invalid.ToException();
        }

        if (request.From > request.To)
        {
            throw ForecastErrors.InvalidRange.ToException();
        }

        // Round to ~1 km / 50 m so nearby requests (everyone at the same trailhead) share one upstream call.
        var lat = Math.Round(request.Latitude, 2);
        var lon = Math.Round(request.Longitude, 2);
        var elevation = request.ElevationMeters is { } e ? Math.Round(e / 50) * 50 : (double?)null;
        var key = $"forecast:{lat:0.00}:{lon:0.00}:{elevation?.ToString("0") ?? "auto"}";

        var forecast = await cache.GetAsync(key, cancellationToken);
        if (forecast is null)
        {
            forecast = await provider.GetForecastAsync(lat, lon, elevation, MaxForecastDays, cancellationToken);
            await cache.SetAsync(key, forecast, CacheDuration, cancellationToken);
        }

        var days = forecast.Daily
            .Where(d => (request.From is null || d.Date >= request.From) && (request.To is null || d.Date <= request.To))
            .Select(d => new DailyForecastResponse(d, d.Condition, HikingConditions.Assess(d)))
            .ToList();

        return new ForecastResponse(forecast.Latitude, forecast.Longitude, forecast.ElevationMeters, forecast.TimeZone, forecast.Current, forecast.Current.Condition, days);
    }
}
