using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using TrailHub.Weather.Application.Interfaces.Providers;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Infrastructure.Providers.OpenMeteo;

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

        var response = await http.GetFromJsonAsync<OpenMeteoForecastResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException("Open-Meteo returned an empty response.");
        return response.ToForecast(elevationMeters);
    }
}
