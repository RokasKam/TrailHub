using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Application.Interfaces.Providers;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal static class PhotoRemoval
{
    /// <summary>
    /// After a photo was removed from its trail: deletes its reports, saves, then deletes the stored file (best effort;
    /// photos linked from elsewhere have none).
    /// </summary>
    public static async Task CompleteAsync(TrailPhoto photo, IPhotoReportRepository reports, IPhotoStorage storage, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        foreach (var report in await reports.GetAllAsync(photo.Id, cancellationToken))
        {
            reports.Remove(report);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (photo.IsStored)
        {
            await storage.DeleteAsync(photo.Url, cancellationToken);
        }
    }
}
