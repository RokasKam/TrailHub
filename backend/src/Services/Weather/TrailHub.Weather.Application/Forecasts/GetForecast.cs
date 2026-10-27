using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Application.Forecasts;

public interface IWeatherProvider
{
    Task<Forecast> GetForecastAsync(double latitude, double longitude, double? elevationMeters, int days, CancellationToken cancellationToken);
}

public interface IForecastCache
{
    Task<Forecast?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, Forecast forecast, TimeSpan timeToLive, CancellationToken cancellationToken);
}

public sealed record DailyForecastDto(DailyForecast Forecast, WeatherCondition Condition, HikingAssessment Assessment);

public sealed record ForecastDto(
    double Latitude,
    double Longitude,
    double? ElevationMeters,
    string TimeZone,
    CurrentConditions Current,
    WeatherCondition CurrentCondition,
    IReadOnlyList<DailyForecastDto> Daily);

/// <param name="From">Optional first day to return (e.g. a planned trip's start), in the location's time zone.</param>
/// <param name="To">Optional last day to return.</param>
/// <param name="ElevationMeters">Trail/summit elevation. Forecasts are downscaled to it, which matters a lot in mountains.</param>
public sealed record GetForecastQuery(double Latitude, double Longitude, double? ElevationMeters, DateOnly? From, DateOnly? To) : IQuery<ForecastDto>;

internal sealed class GetForecastHandler(IWeatherProvider provider, IForecastCache cache) : IQueryHandler<GetForecastQuery, ForecastDto>
{
    public const int MaxForecastDays = 16;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(20);

    public async Task<Result<ForecastDto>> Handle(GetForecastQuery query, CancellationToken cancellationToken)
    {
        if (GeoPoint.Create(query.Latitude, query.Longitude).Error is { } invalid)
        {
            return invalid;
        }

        if (query.From > query.To)
        {
            return Error.Validation("Forecast.InvalidRange", "'from' must not be after 'to'.");
        }

        // Round to ~1 km / 50 m so nearby requests (everyone at the same trailhead) share one upstream call.
        var lat = Math.Round(query.Latitude, 2);
        var lon = Math.Round(query.Longitude, 2);
        var elevation = query.ElevationMeters is { } e ? Math.Round(e / 50) * 50 : (double?)null;
        var key = $"forecast:{lat:0.00}:{lon:0.00}:{elevation?.ToString("0") ?? "auto"}";

        var forecast = await cache.GetAsync(key, cancellationToken);
        if (forecast is null)
        {
            forecast = await provider.GetForecastAsync(lat, lon, elevation, MaxForecastDays, cancellationToken);
            await cache.SetAsync(key, forecast, CacheDuration, cancellationToken);
        }

        var days = forecast.Daily
            .Where(d => (query.From is null || d.Date >= query.From) && (query.To is null || d.Date <= query.To))
            .Select(d => new DailyForecastDto(d, d.Condition, HikingConditions.Assess(d)))
            .ToList();

        return new ForecastDto(forecast.Latitude, forecast.Longitude, forecast.ElevationMeters, forecast.TimeZone, forecast.Current, forecast.Current.Condition, days);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddWeatherApplication(this IServiceCollection services) =>
        services.AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);
}
