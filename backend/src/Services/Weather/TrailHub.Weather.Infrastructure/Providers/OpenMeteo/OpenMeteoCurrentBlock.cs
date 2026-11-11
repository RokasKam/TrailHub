using System.Text.Json.Serialization;

namespace TrailHub.Weather.Infrastructure.Providers.OpenMeteo;

/// <summary>The <c>current</c> block of an Open-Meteo forecast response.</summary>
internal sealed record OpenMeteoCurrentBlock(
    string Time,
    [property: JsonPropertyName("temperature_2m")] double Temperature2m,
    [property: JsonPropertyName("apparent_temperature")] double ApparentTemperature,
    [property: JsonPropertyName("relative_humidity_2m")] int RelativeHumidity2m,
    double Precipitation,
    [property: JsonPropertyName("weather_code")] int WeatherCode,
    [property: JsonPropertyName("wind_speed_10m")] double WindSpeed10m,
    [property: JsonPropertyName("wind_gusts_10m")] double WindGusts10m,
    [property: JsonPropertyName("is_day")] int IsDay);
