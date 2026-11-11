using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Application.Responses;

public sealed record ForecastResponse(
    double Latitude,
    double Longitude,
    double? ElevationMeters,
    string TimeZone,
    CurrentConditions Current,
    WeatherCondition CurrentCondition,
    IReadOnlyList<DailyForecastResponse> Daily);
