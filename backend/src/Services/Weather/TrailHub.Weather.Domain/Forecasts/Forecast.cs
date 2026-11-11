namespace TrailHub.Weather.Domain.Forecasts;

public sealed record Forecast(
    double Latitude,
    double Longitude,
    double? ElevationMeters,
    string TimeZone,
    CurrentConditions Current,
    IReadOnlyList<DailyForecast> Daily);
