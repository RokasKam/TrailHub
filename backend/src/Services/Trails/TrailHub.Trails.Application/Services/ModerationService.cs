using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trails.Application.Interfaces.Providers;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class ModerationService(
    ITrailRepository trails,
    ITrailReportRepository reports,
    IModerators moderators,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IModerationService
{
    public async Task ReportAsync(Guid trailId, ReportTrailRequest request, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        var now = clock.GetUtcNow().UtcDateTime;
        if (await reports.GetByUserAsync(trail.Id, user.UserId, cancellationToken) is { } existing)
        {
            existing.Update(request.Reason, request.Details, now).ThrowIfFailure();
        }
        else
        {
            reports.Add(TrailReport.Create(trail, user.UserId, request.Reason, request.Details, now).GetValueOrThrow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (await reports.CountOpenAsync(trail.Id, cancellationToken) >= Trail.AutoHideReportCount && !trail.IsHidden)
        {
            trail.Hide(HiddenBy.Reports, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken)
    {
        EnsureModerator();
        return await reports.GetQueueAsync(cancellationToken);
    }

    public async Task ModerateAsync(Guid trailId, ModerateTrailRequest request, CancellationToken cancellationToken)
    {
        EnsureModerator();
        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.Hidden)
        {
            trail.Hide(HiddenBy.Moderator, now);
        }
        else
        {
            trail.Unhide(HiddenBy.Moderator);
        }

        foreach (var report in await reports.GetOpenAsync(trail.Id, cancellationToken))
        {
            report.Resolve(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void EnsureModerator()
    {
        if (!moderators.IsModerator(user.UserId))
        {
            throw TrailErrors.NotModerator.ToException();
        }
    }
}
