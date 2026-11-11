using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Interfaces.Repository;

public interface IReviewRepository
{
    Task<Review?> GetByUserAsync(Guid trailId, string userId, CancellationToken cancellationToken);

    void Add(Review review);

    Task<PagedResult<Review>> GetPageAsync(Guid trailId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Rating sum and count for a trail, ignoring <paramref name="excludingUserId"/>'s review.</summary>
    Task<(int Sum, int Count)> GetRatingTotalsAsync(Guid trailId, string excludingUserId, CancellationToken cancellationToken);
}
