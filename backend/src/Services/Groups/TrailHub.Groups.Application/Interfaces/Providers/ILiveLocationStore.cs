using TrailHub.Groups.Application.Dtos;

namespace TrailHub.Groups.Application.Interfaces.Providers;

/// <summary>
/// Latest position per member. Ephemeral by design: entries expire on their own, so a phone that
/// loses signal simply drops off the map instead of showing a stale position forever.
/// </summary>
public interface ILiveLocationStore
{
    static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(15);

    Task SetAsync(Guid groupId, LiveLocationDto location, CancellationToken cancellationToken);

    Task<IReadOnlyList<LiveLocationDto>> GetAsync(Guid groupId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);

    Task RemoveAsync(Guid groupId, string userId, CancellationToken cancellationToken);
}
