using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TrailHub.Groups.Application.Dtos;
using TrailHub.Groups.Application.Interfaces.Providers;

namespace TrailHub.Groups.Infrastructure.LiveLocations;

/// <summary>One Redis key per (group, member) with a sliding TTL; falls back to in-memory when Redis isn't configured.</summary>
internal sealed class DistributedLiveLocationStore(IDistributedCache cache) : ILiveLocationStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task SetAsync(Guid groupId, LiveLocationDto location, CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            Key(groupId, location.UserId),
            JsonSerializer.Serialize(location, Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ILiveLocationStore.TimeToLive },
            cancellationToken);

    public async Task<IReadOnlyList<LiveLocationDto>> GetAsync(Guid groupId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        var results = new List<LiveLocationDto>(userIds.Count);
        foreach (var userId in userIds)
        {
            if (await cache.GetStringAsync(Key(groupId, userId), cancellationToken) is { } json &&
                JsonSerializer.Deserialize<LiveLocationDto>(json, Json) is { } location)
            {
                results.Add(location);
            }
        }

        return results;
    }

    public Task RemoveAsync(Guid groupId, string userId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(Key(groupId, userId), cancellationToken);

    private static string Key(Guid groupId, string userId) => $"live-location:{groupId:N}:{userId}";
}
