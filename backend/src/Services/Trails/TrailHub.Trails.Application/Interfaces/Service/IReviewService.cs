using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>Trail reviews and the trail's rating summary.</summary>
public interface IReviewService
{
    Task<PagedResult<ReviewResponse>> GetReviewsAsync(Guid trailId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Creates or updates the caller's review (one per user per trail) and refreshes the trail's rating.</summary>
    Task<ReviewResponse> UpsertMineAsync(Guid trailId, ReviewRequest request, CancellationToken cancellationToken);
}
