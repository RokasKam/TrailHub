using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Application.Interfaces.Repository;

public interface IHikeLikeRepository
{
    Task<HikeLike?> GetAsync(Guid activityId, string userId, CancellationToken cancellationToken);

    void Add(HikeLike like);

    void Remove(HikeLike like);

    Task<IReadOnlyDictionary<Guid, int>> GetCountsAsync(IReadOnlyCollection<Guid> activityIds, CancellationToken cancellationToken);

    /// <summary>Which of the hikes <paramref name="userId"/> has liked.</summary>
    Task<IReadOnlySet<Guid>> GetLikedAsync(IReadOnlyCollection<Guid> activityIds, string userId, CancellationToken cancellationToken);
}
