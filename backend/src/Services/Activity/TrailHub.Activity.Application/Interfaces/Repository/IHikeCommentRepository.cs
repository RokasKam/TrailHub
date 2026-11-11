using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Application.Interfaces.Repository;

public interface IHikeCommentRepository
{
    Task<HikeComment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A hike's comments, oldest first.</summary>
    Task<IReadOnlyList<HikeComment>> GetForActivityAsync(Guid activityId, CancellationToken cancellationToken);

    /// <summary>Comment counts per hike: hidden comments only count for their writer, or for everyone when <paramref name="includeHidden"/>.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetCountsAsync(IReadOnlyCollection<Guid> activityIds, string viewerId, bool includeHidden, CancellationToken cancellationToken);

    Task<IReadOnlyList<HikeComment>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Comments hidden after reports or by a moderator.</summary>
    Task<IReadOnlyList<HikeComment>> GetHiddenAsync(CancellationToken cancellationToken);

    void Add(HikeComment comment);

    void Remove(HikeComment comment);
}
