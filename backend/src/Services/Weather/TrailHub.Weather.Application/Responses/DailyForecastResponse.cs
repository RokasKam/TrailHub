using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Application.Responses;

public sealed record DailyForecastResponse(DailyForecast Forecast, WeatherCondition Condition, HikingAssessment Assessment);
