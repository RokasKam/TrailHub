namespace TrailHub.Weather.Domain.Forecasts;

public enum WeatherCondition
{
    Clear,
    PartlyCloudy,
    Overcast,
    Fog,
    Drizzle,
    Rain,
    FreezingRain,
    Snow,
    Showers,
    Thunderstorm,
    Unknown,
}

public enum HikingSuitability
{
    Good,
    Caution,
    Poor,
}

public sealed record CurrentConditions(
    DateTime ObservedAtLocal,
    double TemperatureC,
    double ApparentTemperatureC,
    int RelativeHumidityPercent,
    double PrecipitationMm,
    double WindSpeedKmh,
    double WindGustsKmh,
    int WeatherCode,
    bool IsDay)
{
    public WeatherCondition Condition => WmoCodes.ToCondition(WeatherCode);
}

public sealed record DailyForecast(
    DateOnly Date,
    int WeatherCode,
    double MinTemperatureC,
    double MaxTemperatureC,
    double PrecipitationMm,
    int PrecipitationProbabilityPercent,
    double MaxWindSpeedKmh,
    double MaxWindGustsKmh,
    double UvIndexMax,
    DateTime SunriseLocal,
    DateTime SunsetLocal)
{
    public WeatherCondition Condition => WmoCodes.ToCondition(WeatherCode);
}

public sealed record Forecast(
    double Latitude,
    double Longitude,
    double? ElevationMeters,
    string TimeZone,
    CurrentConditions Current,
    IReadOnlyList<DailyForecast> Daily);

public sealed record HikingAssessment(HikingSuitability Suitability, IReadOnlyList<string> Warnings);

/// <summary>WMO weather interpretation codes as used by Open-Meteo and most national services.</summary>
public static class WmoCodes
{
    public static WeatherCondition ToCondition(int code) => code switch
    {
        0 => WeatherCondition.Clear,
        1 or 2 => WeatherCondition.PartlyCloudy,
        3 => WeatherCondition.Overcast,
        45 or 48 => WeatherCondition.Fog,
        51 or 53 or 55 => WeatherCondition.Drizzle,
        56 or 57 or 66 or 67 => WeatherCondition.FreezingRain,
        61 or 63 or 65 => WeatherCondition.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherCondition.Snow,
        80 or 81 or 82 => WeatherCondition.Showers,
        95 or 96 or 99 => WeatherCondition.Thunderstorm,
        _ => WeatherCondition.Unknown,
    };
}

/// <summary>
/// Rule-of-thumb hiking assessment for a day. Thresholds are conservative defaults for exposed
/// mountain terrain; they are guidance for planning, not a safety guarantee.
/// </summary>
public static class HikingConditions
{
    public static HikingAssessment Assess(DailyForecast day)
    {
        var poor = new List<string>();
        var caution = new List<string>();

        switch (day.Condition)
        {
            case WeatherCondition.Thunderstorm:
                poor.Add("Thunderstorms expected: avoid ridges and summits.");
                break;
            case WeatherCondition.FreezingRain:
                poor.Add("Freezing rain: trails will be icy.");
                break;
            case WeatherCondition.Snow:
                caution.Add("Snowfall: check trail conditions and bring traction.");
                break;
            case WeatherCondition.Fog:
                caution.Add("Fog: navigation may be difficult.");
                break;
        }

        if (day.MaxWindGustsKmh >= 70)
        {
            poor.Add($"Gusts up to {day.MaxWindGustsKmh:0} km/h.");
        }
        else if (day.MaxWindGustsKmh >= 45)
        {
            caution.Add($"Strong gusts up to {day.MaxWindGustsKmh:0} km/h on exposed sections.");
        }

        if (day.PrecipitationProbabilityPercent >= 70 && day.PrecipitationMm >= 10)
        {
            poor.Add($"Heavy rain likely ({day.PrecipitationMm:0} mm).");
        }
        else if (day.PrecipitationProbabilityPercent >= 40)
        {
            caution.Add($"{day.PrecipitationProbabilityPercent}% chance of precipitation.");
        }

        if (day.MaxTemperatureC >= 32)
        {
            caution.Add($"Heat: up to {day.MaxTemperatureC:0}°C. Carry extra water.");
        }

        if (day.MinTemperatureC <= -10)
        {
            caution.Add($"Cold: down to {day.MinTemperatureC:0}°C.");
        }

        if (day.UvIndexMax >= 8)
        {
            caution.Add($"Very high UV index ({day.UvIndexMax:0}).");
        }

        var suitability = poor.Count > 0 ? HikingSuitability.Poor
            : caution.Count > 0 ? HikingSuitability.Caution
            : HikingSuitability.Good;
        return new HikingAssessment(suitability, [.. poor, .. caution]);
    }
}
