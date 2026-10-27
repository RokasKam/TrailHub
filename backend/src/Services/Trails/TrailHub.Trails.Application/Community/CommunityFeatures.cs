using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trails.Application.Abstractions;
using TrailHub.Trails.Application.Trails;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Community;

public sealed record ReviewDto(Guid Id, string UserId, int Rating, string? Comment, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed record UpsertReviewCommand(Guid TrailId, int Rating, string? Comment) : ICommand<ReviewDto>;

internal sealed class UpsertReviewHandler(
    ITrailRepository trails,
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock)
    : ICommandHandler<UpsertReviewCommand, ReviewDto>
{
    public async Task<Result<ReviewDto>> Handle(UpsertReviewCommand command, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null)
        {
            return TrailErrors.NotFound;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var review = await reviews.GetByUserAsync(trail.Id, user.UserId, cancellationToken);
        if (review is null)
        {
            var created = Review.Create(trail.Id, user.UserId, command.Rating, command.Comment, now);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            review = created.Value;
            reviews.Add(review);
        }
        else if (review.Update(command.Rating, command.Comment, now) is { IsFailure: true } failed)
        {
            return failed.Error!;
        }

        // Others' totals + this review: keeps the summary exact without a second round trip after saving.
        var (sum, count) = await reviews.GetRatingTotalsAsync(trail.Id, user.UserId, cancellationToken);
        trail.UpdateRatingSummary((sum + review.Rating) / (double)(count + 1), count + 1);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return review.ToDto();
    }
}

public sealed record GetReviewsQuery(Guid TrailId, PageRequest Page) : IQuery<PagedResult<ReviewDto>>;

internal sealed class GetReviewsHandler(IReviewRepository reviews) : IQueryHandler<GetReviewsQuery, PagedResult<ReviewDto>>
{
    public async Task<Result<PagedResult<ReviewDto>>> Handle(GetReviewsQuery query, CancellationToken cancellationToken)
    {
        var page = await reviews.GetPageAsync(query.TrailId, query.Page, cancellationToken);
        return new PagedResult<ReviewDto>(page.Items.Select(r => r.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed record ReportConditionCommand(Guid TrailId, TrailCondition Condition, string? Note) : ICommand<ConditionReportDto>;

internal sealed class ReportConditionHandler(
    ITrailRepository trails,
    IConditionReportRepository reports,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock)
    : ICommandHandler<ReportConditionCommand, ConditionReportDto>
{
    public async Task<Result<ConditionReportDto>> Handle(ReportConditionCommand command, CancellationToken cancellationToken)
    {
        if (!await trails.ExistsAsync(command.TrailId, cancellationToken))
        {
            return TrailErrors.NotFound;
        }

        var report = ConditionReport.Create(command.TrailId, user.UserId, command.Condition, command.Note, clock.GetUtcNow().UtcDateTime);
        if (report.IsFailure)
        {
            return report.Error!;
        }

        reports.Add(report.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return report.Value.ToDto();
    }
}

public sealed record GetConditionReportsQuery(Guid TrailId, int Take = 20) : IQuery<IReadOnlyList<ConditionReportDto>>;

internal sealed class GetConditionReportsHandler(IConditionReportRepository reports)
    : IQueryHandler<GetConditionReportsQuery, IReadOnlyList<ConditionReportDto>>
{
    public async Task<Result<IReadOnlyList<ConditionReportDto>>> Handle(GetConditionReportsQuery query, CancellationToken cancellationToken)
    {
        var recent = await reports.GetRecentAsync(query.TrailId, Math.Clamp(query.Take, 1, 100), cancellationToken);
        return recent.Select(r => r.ToDto()).ToList();
    }
}

public sealed record SetFavoriteCommand(Guid TrailId, bool IsFavorite) : ICommand;

/// <summary>Idempotent: favoriting twice or un-favoriting a non-favorite both succeed.</summary>
internal sealed class SetFavoriteHandler(
    ITrailRepository trails,
    IFavoriteTrailRepository favorites,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock)
    : ICommandHandler<SetFavoriteCommand>
{
    public async Task<Result> Handle(SetFavoriteCommand command, CancellationToken cancellationToken)
    {
        var existing = await favorites.GetAsync(user.UserId, command.TrailId, cancellationToken);
        if (command.IsFavorite && existing is null)
        {
            if (!await trails.ExistsAsync(command.TrailId, cancellationToken))
            {
                return TrailErrors.NotFound;
            }

            favorites.Add(FavoriteTrail.Create(user.UserId, command.TrailId, clock.GetUtcNow().UtcDateTime));
        }
        else if (!command.IsFavorite && existing is not null)
        {
            favorites.Remove(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GetFavoriteTrailsQuery : IQuery<IReadOnlyList<TrailSummaryDto>>;

internal sealed class GetFavoriteTrailsHandler(IFavoriteTrailRepository favorites, ITrailRepository trails, ICurrentUser user)
    : IQueryHandler<GetFavoriteTrailsQuery, IReadOnlyList<TrailSummaryDto>>
{
    public async Task<Result<IReadOnlyList<TrailSummaryDto>>> Handle(GetFavoriteTrailsQuery query, CancellationToken cancellationToken)
    {
        var ids = await favorites.GetTrailIdsAsync(user.UserId, cancellationToken);
        return ids.Count == 0 ? Result<IReadOnlyList<TrailSummaryDto>>.Success([]) : Result<IReadOnlyList<TrailSummaryDto>>.Success(await trails.GetSummariesAsync(ids, cancellationToken));
    }
}

internal static class CommunityMappings
{
    public static ReviewDto ToDto(this Review r) => new(r.Id, r.UserId, r.Rating, r.Comment, r.CreatedAtUtc, r.UpdatedAtUtc);
}
