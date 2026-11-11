using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Application.Interfaces.Caching;

public interface IForecastCache
{
    Task<Forecast?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, Forecast forecast, TimeSpan timeToLive, CancellationToken cancellationToken);
}
