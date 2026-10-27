using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.Weather.Application.Forecasts;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Infrastructure;

/// <summary>
/// <see href="https://open-meteo.com/en/docs">Open-Meteo</see>: free for non-commercial use, no API key.
/// For commercial use, buy a plan and set <c>Weather:OpenMeteo:BaseUrl</c> / <c>ApiKey</c>.
/// </summary>
internal sealed class OpenMeteoWeatherProvider(HttpClient http, IConfiguration configuration) : IWeatherProvider
{
    private const string Current = "temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,weather_code,wind_speed_10m,wind_gusts_10m,is_day";
    private const string Daily = "weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,wind_speed_10m_max,wind_gusts_10m_max,uv_index_max,sunrise,sunset";

    public async Task<Forecast> GetForecastAsync(double latitude, double longitude, double? elevationMeters, int days, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={latitude}&longitude={longitude}&current={Current}&daily={Daily}&timezone=auto&forecast_days={days}");
        if (elevationMeters is { } elevation)
        {
            url += string.Create(CultureInfo.InvariantCulture, $"&elevation={elevation}");
        }

        if (configuration["Weather:OpenMeteo:ApiKey"] is { Length: > 0 } apiKey)
        {
            url += $"&apikey={Uri.EscapeDataString(apiKey)}";
        }

        var response = await http.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException("Open-Meteo returned an empty response.");
        return response.ToForecast(elevationMeters);
    }

    private sealed record OpenMeteoResponse(
        double Latitude,
        double Longitude,
        double? Elevation,
        string Timezone,
        CurrentBlock Current,
        DailyBlock Daily)
    {
        public Forecast ToForecast(double? requestedElevation) => new(
            Latitude,
            Longitude,
            requestedElevation ?? Elevation,
            Timezone,
            new CurrentConditions(
                DateTime.Parse(Current.Time, CultureInfo.InvariantCulture),
                Current.Temperature2m,
                Current.ApparentTemperature,
                Current.RelativeHumidity2m,
                Current.Precipitation,
                Current.WindSpeed10m,
                Current.WindGusts10m,
                Current.WeatherCode,
                Current.IsDay == 1),
            Enumerable.Range(0, Daily.Time.Count)
                .Select(i => new DailyForecast(
                    DateOnly.Parse(Daily.Time[i], CultureInfo.InvariantCulture),
                    Daily.WeatherCode[i] ?? -1,
                    Daily.Temperature2mMin[i] ?? 0,
                    Daily.Temperature2mMax[i] ?? 0,
                    Daily.PrecipitationSum[i] ?? 0,
                    Daily.PrecipitationProbabilityMax[i] ?? 0,
                    Daily.WindSpeed10mMax[i] ?? 0,
                    Daily.WindGusts10mMax[i] ?? 0,
                    Daily.UvIndexMax[i] ?? 0,
                    DateTime.Parse(Daily.Sunrise[i], CultureInfo.InvariantCulture),
                    DateTime.Parse(Daily.Sunset[i], CultureInfo.InvariantCulture)))
                .ToList());
    }

    private sealed record CurrentBlock(
        string Time,
        [property: JsonPropertyName("temperature_2m")] double Temperature2m,
        [property: JsonPropertyName("apparent_temperature")] double ApparentTemperature,
        [property: JsonPropertyName("relative_humidity_2m")] int RelativeHumidity2m,
        double Precipitation,
        [property: JsonPropertyName("weather_code")] int WeatherCode,
        [property: JsonPropertyName("wind_speed_10m")] double WindSpeed10m,
        [property: JsonPropertyName("wind_gusts_10m")] double WindGusts10m,
        [property: JsonPropertyName("is_day")] int IsDay);

    // Daily values can be null far out in the forecast range, hence the nullable arrays.
    private sealed record DailyBlock(
        IReadOnlyList<string> Time,
        [property: JsonPropertyName("weather_code")] IReadOnlyList<int?> WeatherCode,
        [property: JsonPropertyName("temperature_2m_max")] IReadOnlyList<double?> Temperature2mMax,
        [property: JsonPropertyName("temperature_2m_min")] IReadOnlyList<double?> Temperature2mMin,
        [property: JsonPropertyName("precipitation_sum")] IReadOnlyList<double?> PrecipitationSum,
        [property: JsonPropertyName("precipitation_probability_max")] IReadOnlyList<int?> PrecipitationProbabilityMax,
        [property: JsonPropertyName("wind_speed_10m_max")] IReadOnlyList<double?> WindSpeed10mMax,
        [property: JsonPropertyName("wind_gusts_10m_max")] IReadOnlyList<double?> WindGusts10mMax,
        [property: JsonPropertyName("uv_index_max")] IReadOnlyList<double?> UvIndexMax,
        IReadOnlyList<string> Sunrise,
        IReadOnlyList<string> Sunset);
}

internal sealed class DistributedForecastCache(IDistributedCache cache) : IForecastCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Forecast?> GetAsync(string key, CancellationToken cancellationToken) =>
        await cache.GetStringAsync(key, cancellationToken) is { } json ? JsonSerializer.Deserialize<Forecast>(json, Json) : null;

    public Task SetAsync(string key, Forecast forecast, TimeSpan timeToLive, CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(forecast, Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive },
            cancellationToken);
}

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
