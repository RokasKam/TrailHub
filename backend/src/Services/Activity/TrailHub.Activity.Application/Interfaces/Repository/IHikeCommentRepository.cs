using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Application.Interfaces.Repository;

public interface IHikeCommentRepository
{
    Task<HikeComment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A hike's comments, oldest first.</summary>
    Task<IReadOnlyList<HikeComment>> GetForActivityAsync(Guid activityId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> GetCountsAsync(IReadOnlyCollection<Guid> activityIds, CancellationToken cancellationToken);

    void Add(HikeComment comment);

    void Remove(HikeComment comment);
}
