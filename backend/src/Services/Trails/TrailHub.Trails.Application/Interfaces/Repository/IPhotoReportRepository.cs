using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Interfaces.Repository;

public interface IPhotoReportRepository
{
    Task<PhotoReport?> GetByUserAsync(Guid photoId, string userId, CancellationToken cancellationToken);

    Task<int> CountOpenAsync(Guid photoId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PhotoReport>> GetOpenAsync(Guid photoId, CancellationToken cancellationToken);

    /// <summary>All of a photo's reports, open or resolved (they're deleted with the photo).</summary>
    Task<IReadOnlyList<PhotoReport>> GetAllAsync(Guid photoId, CancellationToken cancellationToken);

    /// <summary>
    /// Photos with open reports or hidden, most reported first. <see cref="PhotoModerationItemResponse.Url"/> is the
    /// stored key or external URL, as saved.
    /// </summary>
    Task<IReadOnlyList<PhotoModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken);

    void Add(PhotoReport report);

    void Remove(PhotoReport report);
}
