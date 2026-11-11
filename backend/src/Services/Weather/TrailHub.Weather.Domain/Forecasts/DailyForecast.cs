namespace TrailHub.Weather.Domain.Forecasts;

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
