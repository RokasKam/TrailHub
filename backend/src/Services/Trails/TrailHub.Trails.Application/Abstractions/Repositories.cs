using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Application.Trails;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Abstractions;

public interface ITrailRepository
{
    Task<Trail?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    void Add(Trail trail);

    /// <summary>Read-side projection: returns summaries without loading full routes.</summary>
    Task<PagedResult<TrailSummaryDto>> SearchAsync(TrailSearchCriteria criteria, PageRequest page, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrailSummaryDto>> GetSummariesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

public interface IReviewRepository
{
    Task<Review?> GetByUserAsync(Guid trailId, string userId, CancellationToken cancellationToken);

    void Add(Review review);

    Task<PagedResult<Review>> GetPageAsync(Guid trailId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Rating sum and count for a trail, ignoring <paramref name="excludingUserId"/>'s review.</summary>
    Task<(int Sum, int Count)> GetRatingTotalsAsync(Guid trailId, string excludingUserId, CancellationToken cancellationToken);
}

public interface IConditionReportRepository
{
    void Add(ConditionReport report);

    Task<IReadOnlyList<ConditionReport>> GetRecentAsync(Guid trailId, int take, CancellationToken cancellationToken);
}

public interface IFavoriteTrailRepository
{
    Task<FavoriteTrail?> GetAsync(string userId, Guid trailId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetTrailIdsAsync(string userId, CancellationToken cancellationToken);

    void Add(FavoriteTrail favorite);

    void Remove(FavoriteTrail favorite);
}
