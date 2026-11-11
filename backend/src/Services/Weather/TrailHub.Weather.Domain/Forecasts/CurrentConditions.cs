namespace TrailHub.Weather.Domain.Forecasts;

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
