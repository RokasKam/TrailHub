using TrailHub.Activity.Application.Interfaces.Providers;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Mappings;
using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;

namespace TrailHub.Activity.Application.Services;

internal sealed class HikePhotoService(
    IActivityRepository activities,
    IContentReportRepository reports,
    IHikePhotoStorage storage,
    IModerators moderators,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IHikePhotoService
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/jpg"] = "jpg",
        ["image/png"] = "png",
        ["image/heic"] = "heic",
        ["image/heif"] = "heic",
        ["image/webp"] = "webp",
    };

    public async Task<HikePhotoResponse> UploadAsync(Guid id, Stream content, string contentType, long length, string? caption, CancellationToken cancellationToken)
    {
        if (!Extensions.TryGetValue(contentType, out var extension) || length <= 0)
        {
            throw ActivityErrors.UnsupportedPhoto.ToException();
        }

        if (length > HikeActivity.MaxPhotoBytes)
        {
            throw ActivityErrors.PhotoTooLarge.ToException();
        }

        var activity = await activities.GetOwnAsync(id, user.UserId, cancellationToken);
        var key = $"{HikeActivity.PhotoKeyPrefix(activity.Id)}{Guid.CreateVersion7():N}.{extension}";
        var photo = activity.AddPhoto(key, caption, clock.GetUtcNow().UtcDateTime).GetValueOrThrow();

        // Upload first: if storage fails nothing is saved; if the DB save fails we only leave an orphaned object.
        await storage.SaveAsync(key, content, contentType, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return photo.ToResponse(storage);
    }

    public async Task DeleteAsync(Guid id, Guid photoId, CancellationToken cancellationToken)
    {
        // Moderators can delete any hike's photos; to everyone else other people's hikes don't exist here.
        var activity = moderators.IsModerator(user.UserId)
            ? await activities.GetByIdAsync(id, cancellationToken) ?? throw ActivityErrors.NotFound.ToException()
            : await activities.GetOwnAsync(id, user.UserId, cancellationToken);
        var photo = activity.RemovePhoto(photoId).GetValueOrThrow();
        await ContentRemoval.CompletePhotoAsync(photo, reports, storage, unitOfWork, cancellationToken);
    }
}
