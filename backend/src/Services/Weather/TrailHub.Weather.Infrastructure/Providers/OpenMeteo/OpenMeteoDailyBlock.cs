using System.Text.Json.Serialization;

namespace TrailHub.Weather.Infrastructure.Providers.OpenMeteo;

/// <summary>The <c>daily</c> block of an Open-Meteo forecast response.</summary>
// Daily values can be null far out in the forecast range, hence the nullable arrays.
internal sealed record OpenMeteoDailyBlock(
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
