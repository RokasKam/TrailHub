namespace TrailHub.Weather.Domain.Forecasts;

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
