using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Weather.Domain.Forecasts;

public static class ForecastErrors
{
    public static readonly Error InvalidRange = Error.Validation("Forecast.InvalidRange", "'from' must not be after 'to'.");
}
