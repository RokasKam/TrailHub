using TrailHub.Activity.Application.Interfaces.Providers;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Activity.Application.Services;

internal static class ContentRemoval
{
    /// <summary>After a photo was detached from its hike: deletes its reports, saves, then deletes the stored file (best effort).</summary>
    public static async Task CompletePhotoAsync(HikePhoto photo, IContentReportRepository reports, IHikePhotoStorage storage, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await RemoveReportsAsync(photo.Id, reports, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(photo.Key, cancellationToken);
    }

    /// <summary>Deletes every report on a comment or photo that's being deleted (saved with the caller's changes).</summary>
    public static async Task RemoveReportsAsync(Guid contentId, IContentReportRepository reports, CancellationToken cancellationToken)
    {
        foreach (var report in await reports.GetAllAsync(contentId, cancellationToken))
        {
            reports.Remove(report);
        }
    }
}
