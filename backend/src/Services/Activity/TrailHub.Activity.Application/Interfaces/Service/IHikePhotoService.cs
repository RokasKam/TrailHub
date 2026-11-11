using TrailHub.Activity.Application.Responses;

namespace TrailHub.Activity.Application.Interfaces.Service;

/// <summary>Photos on the caller's hikes.</summary>
public interface IHikePhotoService
{
    Task<HikePhotoResponse> UploadAsync(Guid id, Stream content, string contentType, long length, string? caption, CancellationToken cancellationToken);

    /// <summary>Removes the photo from the hike and deletes the stored file (best effort).</summary>
    Task DeleteAsync(Guid id, Guid photoId, CancellationToken cancellationToken);
}
