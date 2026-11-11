using TrailHub.Activity.Application.Interfaces.Providers;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;

namespace TrailHub.Activity.Application.Services;

internal sealed class ContentModerationService(
    IActivityRepository activities,
    IHikeCommentRepository comments,
    IContentReportRepository reports,
    IGroupDirectory directory,
    IHikePhotoStorage storage,
    IModerators moderators,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IContentModerationService
{
    public async Task ReportCommentAsync(Guid id, Guid commentId, ReportContentRequest request, CancellationToken cancellationToken)
    {
        var (activity, _) = await activities.GetVisibleAsync(directory, id, user.UserId, cancellationToken);
        var comment = await comments.GetByIdAsync(commentId, cancellationToken);
        if (comment is null || comment.ActivityId != activity.Id || !comment.IsVisibleTo(user.UserId, moderators.IsModerator(user.UserId)))
        {
            throw ActivityErrors.CommentNotFound.ToException();
        }

        await FileReportAsync(ContentKind.Comment, activity.Id, comment.Id, comment.UserId, request, cancellationToken);
        if (comment.ApplyReports(await reports.CountOpenAsync(comment.Id, cancellationToken)))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ReportPhotoAsync(Guid id, Guid photoId, ReportContentRequest request, CancellationToken cancellationToken)
    {
        var (activity, _) = await activities.GetVisibleAsync(directory, id, user.UserId, cancellationToken);
        var photo = activity.GetVisiblePhoto(photoId, user.UserId, moderators.IsModerator(user.UserId)).GetValueOrThrow();

        await FileReportAsync(ContentKind.HikePhoto, activity.Id, photo.Id, activity.UserId, request, cancellationToken);
        if (activity.ApplyPhotoReports(photo.Id, await reports.CountOpenAsync(photo.Id, cancellationToken)))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ContentModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken)
    {
        EnsureModerator();
        var open = await reports.GetAllOpenAsync(cancellationToken);
        var openByContent = open.ToLookup(r => r.ContentId);

        // Comments: reported ones and hidden ones.
        var reportedCommentIds = open.Where(r => r.Kind == ContentKind.Comment).Select(r => r.ContentId).Distinct().ToList();
        var queuedComments = (await comments.GetByIdsAsync(reportedCommentIds, cancellationToken))
            .Concat(await comments.GetHiddenAsync(cancellationToken))
            .DistinctBy(c => c.Id)
            .ToList();

        // Photos: on hikes with a reported photo or a hidden one.
        var photoActivityIds = open.Where(r => r.Kind == ContentKind.HikePhoto).Select(r => r.ActivityId)
            .Concat(await activities.GetIdsWithHiddenPhotosAsync(cancellationToken))
            .Distinct()
            .ToList();
        var queuedPhotos = (await activities.GetByIdsAsync(photoActivityIds, cancellationToken))
            .SelectMany(a => a.Photos
                .Where(p => p.HiddenBy is not null || openByContent.Contains(p.Id))
                .Select(p => (Activity: a, Photo: p)))
            .ToList();

        var authorIds = queuedComments.Select(c => c.UserId).Concat(queuedPhotos.Select(x => x.Activity.UserId)).Distinct().ToList();
        var names = authorIds.Count == 0
            ? new Dictionary<string, string>()
            : await directory.GetDisplayNamesAsync(authorIds, cancellationToken);
        string NameOf(string userId) => names.GetValueOrDefault(userId) ?? DisplayNames.Unknown;
        IReadOnlyList<ContentReportResponse> ReportsOf(Guid contentId) =>
            openByContent[contentId].OrderByDescending(r => r.CreatedAtUtc).Select(r => new ContentReportResponse(r.UserId, r.Reason, r.Details, r.CreatedAtUtc)).ToList();

        var items = queuedComments
            .Select(c => new ContentModerationItemResponse(ContentKind.Comment, c.ActivityId, c.Id, c.Text, null, c.UserId, NameOf(c.UserId), c.HiddenBy, openByContent[c.Id].Count(), ReportsOf(c.Id)))
            .Concat(queuedPhotos.Select(x => new ContentModerationItemResponse(
                ContentKind.HikePhoto,
                x.Activity.Id,
                x.Photo.Id,
                null,
                storage.ResolveUrl(x.Photo.Key),
                x.Activity.UserId,
                NameOf(x.Activity.UserId),
                x.Photo.HiddenBy,
                openByContent[x.Photo.Id].Count(),
                ReportsOf(x.Photo.Id))));
        return items
            .OrderByDescending(i => i.OpenReports)
            .ThenByDescending(i => i.Reports.Count > 0 ? i.Reports[0].CreatedAtUtc : DateTime.MinValue)
            .ToList();
    }

    public async Task ModerateCommentAsync(Guid commentId, ModerateContentRequest request, CancellationToken cancellationToken)
    {
        EnsureModerator();
        ValidateAction(request.Action);
        var comment = await comments.GetByIdAsync(commentId, cancellationToken) ?? throw ActivityErrors.CommentNotFound.ToException();
        if (request.Action == ModerationAction.Delete)
        {
            comments.Remove(comment);
            await ContentRemoval.RemoveReportsAsync(comment.Id, reports, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        comment.Moderate(hidden: request.Action == ModerationAction.Hide);
        await ResolveAndSaveAsync(comment.Id, cancellationToken);
    }

    public async Task ModeratePhotoAsync(Guid photoId, ModerateContentRequest request, CancellationToken cancellationToken)
    {
        EnsureModerator();
        ValidateAction(request.Action);
        var activity = await activities.FindByPhotoIdAsync(photoId, cancellationToken) ?? throw ActivityErrors.PhotoNotFound.ToException();
        if (request.Action == ModerationAction.Delete)
        {
            var photo = activity.RemovePhoto(photoId).GetValueOrThrow();
            await ContentRemoval.CompletePhotoAsync(photo, reports, storage, unitOfWork, cancellationToken);
            return;
        }

        activity.ModeratePhoto(photoId, hidden: request.Action == ModerationAction.Hide).ThrowIfFailure();
        await ResolveAndSaveAsync(photoId, cancellationToken);
    }

    /// <summary>Files the caller's report, or updates (and reopens) the one they filed before, and saves.</summary>
    private async Task FileReportAsync(ContentKind kind, Guid activityId, Guid contentId, string contentAuthorId, ReportContentRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (await reports.GetByUserAsync(contentId, user.UserId, cancellationToken) is { } existing)
        {
            existing.Update(request.Reason, request.Details, now).ThrowIfFailure();
        }
        else
        {
            reports.Add(ContentReport.Create(kind, activityId, contentId, contentAuthorId, user.UserId, request.Reason, request.Details, now).GetValueOrThrow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveAndSaveAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var report in await reports.GetOpenAsync(contentId, cancellationToken))
        {
            report.Resolve(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateAction(ModerationAction action)
    {
        if (!Enum.IsDefined(action))
        {
            throw ActivityErrors.InvalidModerationAction.ToException();
        }
    }

    private void EnsureModerator()
    {
        if (!moderators.IsModerator(user.UserId))
        {
            throw ActivityErrors.NotModerator.ToException();
        }
    }
}
