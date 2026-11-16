using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Weather.Domain.Forecasts;

public static class ForecastErrors
{
    public static readonly Error InvalidRange = Error.Validation("Forecast.InvalidRange", "'from' must not be after 'to'.");

    public static readonly Error Unavailable = Error.Unavailable("Forecast.Unavailable", "The weather service isn't responding. Try again in a minute.");
}
