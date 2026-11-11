using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trails.Application.Extensions;
using TrailHub.Trails.Application.Interfaces.Providers;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class PhotoModerationService(
    ITrailRepository trails,
    IPhotoReportRepository reports,
    IPhotoStorage storage,
    IModerators moderators,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IPhotoModerationService
{
    public async Task ReportAsync(Guid trailId, Guid photoId, ReportContentRequest request, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(trailId, cancellationToken);
        if (trail is null || !trail.CanSee(user.UserId, moderators))
        {
            throw TrailErrors.NotFound.ToException();
        }

        var photo = trail.GetVisiblePhoto(photoId, user.UserId, moderators.IsModerator(user.UserId)).GetValueOrThrow();
        var now = clock.GetUtcNow().UtcDateTime;
        if (await reports.GetByUserAsync(photo.Id, user.UserId, cancellationToken) is { } existing)
        {
            existing.Update(request.Reason, request.Details, now).ThrowIfFailure();
        }
        else
        {
            reports.Add(PhotoReport.Create(trail.Id, photo, user.UserId, request.Reason, request.Details, now).GetValueOrThrow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (trail.ApplyPhotoReports(photo.Id, await reports.CountOpenAsync(photo.Id, cancellationToken)))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<PhotoModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken)
    {
        EnsureModerator();
        var queue = await reports.GetQueueAsync(cancellationToken);
        return queue.Select(i => i with { Url = storage.ResolveUrl(i.Url) }).ToList();
    }

    public async Task ModerateAsync(Guid trailId, Guid photoId, ModerateContentRequest request, CancellationToken cancellationToken)
    {
        EnsureModerator();
        if (!Enum.IsDefined(request.Action))
        {
            throw TrailErrors.InvalidModerationAction.ToException();
        }

        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        if (request.Action == ModerationAction.Delete)
        {
            var removed = trail.RemovePhoto(photoId, user.UserId, isModerator: true).GetValueOrThrow();
            await PhotoRemoval.CompleteAsync(removed, reports, storage, unitOfWork, cancellationToken);
            return;
        }

        trail.ModeratePhoto(photoId, hidden: request.Action == ModerationAction.Hide).ThrowIfFailure();
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var report in await reports.GetOpenAsync(photoId, cancellationToken))
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
