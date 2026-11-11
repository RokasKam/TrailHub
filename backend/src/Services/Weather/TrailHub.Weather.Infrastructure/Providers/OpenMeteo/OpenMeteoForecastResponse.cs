using System.Globalization;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Infrastructure.Providers.OpenMeteo;

/// <summary>Body of Open-Meteo's <c>v1/forecast</c> response.</summary>
internal sealed record OpenMeteoForecastResponse(
    double Latitude,
    double Longitude,
    double? Elevation,
    string Timezone,
    OpenMeteoCurrentBlock Current,
    OpenMeteoDailyBlock Daily)
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
