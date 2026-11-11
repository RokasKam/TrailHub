using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Interfaces.Repository;

public interface ITrailReportRepository
{
    Task<TrailReport?> GetByUserAsync(Guid trailId, string userId, CancellationToken cancellationToken);

    Task<int> CountOpenAsync(Guid trailId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrailReport>> GetOpenAsync(Guid trailId, CancellationToken cancellationToken);

    /// <summary>Trails with open reports or hidden by reports or moderators, most reported first.</summary>
    Task<IReadOnlyList<ModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken);

    void Add(TrailReport report);
}
