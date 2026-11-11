using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TrailHub.Weather.Application.Interfaces.Caching;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.Weather.Infrastructure.Caching;

/// <summary>Stores forecasts as JSON in the distributed cache (Redis, or in-memory without a Redis connection string).</summary>
internal sealed class DistributedForecastCache(IDistributedCache cache) : IForecastCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Forecast?> GetAsync(string key, CancellationToken cancellationToken) =>
        await cache.GetStringAsync(key, cancellationToken) is { } json ? JsonSerializer.Deserialize<Forecast>(json, Json) : null;

    public Task SetAsync(string key, Forecast forecast, TimeSpan timeToLive, CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(forecast, Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive },
            cancellationToken);
}
