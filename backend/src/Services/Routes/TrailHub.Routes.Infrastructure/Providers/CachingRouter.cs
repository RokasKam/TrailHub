using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>
/// Remembers routed lines for a day. The editor re-routes the same waypoints often (undo, reorder back, save after
/// preview), and the hosted router has a daily quota.
/// </summary>
internal sealed class CachingRouter(IRouter inner, IDistributedCache cache, string name) : IRouter
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<RoutedTrack>> RouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        var key = Key("route", waypoints);
        if (await GetAsync(key, cancellationToken) is [var hit])
        {
            return hit;
        }

        var result = await inner.RouteAsync(waypoints, cancellationToken);
        if (result.IsSuccess && Complete(result.Value))
        {
            await SetAsync(key, [result.Value], cancellationToken);
        }

        return result;
    }

    public async Task<Result<IReadOnlyList<RoutedTrack>>> AlternativesAsync(GeoPoint from, GeoPoint to, int count, CancellationToken cancellationToken)
    {
        var key = Key($"alternatives{count}", [from, to]);
        if (await GetAsync(key, cancellationToken) is { Count: > 0 } hit)
        {
            return hit;
        }

        var result = await inner.AlternativesAsync(from, to, count, cancellationToken);
        if (result.IsSuccess && result.Value.All(Complete))
        {
            await SetAsync(key, result.Value, cancellationToken);
        }

        return result;
    }

    /// <summary>A line whose elevation lookup failed is served but not remembered, so the next request tries again.</summary>
    private static bool Complete(RoutedTrack routed) => routed.Track.Points.All(p => p.Elevation is not null);

    private string Key(string kind, IEnumerable<GeoPoint> points) =>
        $"{name}:{kind}:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(';', points.Select(p => $"{p.Latitude:F5},{p.Longitude:F5}")))))[..32];

    private async Task<List<RoutedTrack>?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var json = await cache.GetStringAsync(key, cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<List<CachedRoutedTrack>>(json, Json)?.Select(s => s.ToRoutedTrack()).ToList();
    }

    private Task SetAsync(string key, IEnumerable<RoutedTrack> tracks, CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(tracks.Select(CachedRoutedTrack.From).ToList(), Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheFor },
            cancellationToken);
}
