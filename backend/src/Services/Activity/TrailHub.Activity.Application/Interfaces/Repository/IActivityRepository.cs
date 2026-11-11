using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Activity.Application.Interfaces.Repository;

public interface IActivityRepository
{
    Task<HikeActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The hike that has the photo, or null.</summary>
    Task<HikeActivity?> FindByPhotoIdAsync(Guid photoId, CancellationToken cancellationToken);

    /// <summary>Read-only copies of hikes, for the moderators' queue.</summary>
    Task<IReadOnlyList<HikeActivity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Ids of hikes with a hidden photo.</summary>
    Task<IReadOnlyList<Guid>> GetIdsWithHiddenPhotosAsync(CancellationToken cancellationToken);

    void Add(HikeActivity activity);

    void Remove(HikeActivity activity);

    Task<PagedResult<ActivitySummaryResponse>> GetPageAsync(string userId, PageRequest page, CancellationToken cancellationToken);

    Task<ActivityTotalsResponse> GetTotalsAsync(string userId, DateTime? sinceUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Shared hikes, newest share first: those shared with any of <paramref name="groupIds"/>, plus the caller's own
    /// shared hikes when <paramref name="includeOwn"/> is set.
    /// </summary>
    Task<PagedResult<HikeActivity>> GetFeedPageAsync(string userId, IReadOnlyCollection<Guid> groupIds, bool includeOwn, PageRequest page, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlyStatisticsResponse>> GetMonthlyAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken);
}
