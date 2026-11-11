using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>Trail photos: uploads to photo storage and links to photos hosted elsewhere.</summary>
public interface ITrailPhotoService
{
    /// <summary>Stores a JPEG, PNG, WebP or HEIC photo (up to 10 MB) and attaches it to the trail.</summary>
    Task<TrailPhotoResponse> UploadAsync(Guid trailId, Stream content, string contentType, long length, string? caption, CancellationToken cancellationToken);

    Task AddAsync(Guid trailId, AddPhotoRequest request, CancellationToken cancellationToken);

    /// <summary>Removes a photo and its stored file. Allowed for its uploader, the trail's author and moderators.</summary>
    Task DeleteAsync(Guid trailId, Guid photoId, CancellationToken cancellationToken);
}
