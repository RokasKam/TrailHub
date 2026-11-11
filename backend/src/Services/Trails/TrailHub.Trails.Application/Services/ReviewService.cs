using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Mappings;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class ReviewService(
    ITrailRepository trails,
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IReviewService
{
    public async Task<PagedResult<ReviewResponse>> GetReviewsAsync(Guid trailId, PageRequest page, CancellationToken cancellationToken)
    {
        var result = await reviews.GetPageAsync(trailId, page, cancellationToken);
        return new PagedResult<ReviewResponse>(result.Items.Select(r => r.ToResponse()).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<ReviewResponse> UpsertMineAsync(Guid trailId, ReviewRequest request, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        var now = clock.GetUtcNow().UtcDateTime;
        var review = await reviews.GetByUserAsync(trail.Id, user.UserId, cancellationToken);
        if (review is null)
        {
            review = Review.Create(trail.Id, user.UserId, request.Rating, request.Comment, now).GetValueOrThrow();
            reviews.Add(review);
        }
        else
        {
            review.Update(request.Rating, request.Comment, now).ThrowIfFailure();
        }

        // Others' totals + this review: keeps the summary exact without a second round trip after saving.
        var (sum, count) = await reviews.GetRatingTotalsAsync(trail.Id, user.UserId, cancellationToken);
        trail.UpdateRatingSummary((sum + review.Rating) / (double)(count + 1), count + 1);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return review.ToResponse();
    }
}
