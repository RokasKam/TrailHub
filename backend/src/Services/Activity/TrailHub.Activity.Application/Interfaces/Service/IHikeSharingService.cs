using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;

namespace TrailHub.Activity.Application.Interfaces.Service;

/// <summary>Sharing the caller's hikes with their groups.</summary>
public interface IHikeSharingService
{
    /// <summary>The groups the caller belongs to.</summary>
    Task<IReadOnlyList<ShareTargetResponse>> GetShareTargetsAsync(CancellationToken cancellationToken);

    /// <summary>Shares the hike, or updates its groups, caption and privacy (keeping the original share time).</summary>
    Task<HikeSharingResponse> ShareAsync(Guid id, ShareHikeRequest request, CancellationToken cancellationToken);

    /// <summary>Stops sharing; likes and comments are kept.</summary>
    Task UnshareAsync(Guid id, CancellationToken cancellationToken);
}
