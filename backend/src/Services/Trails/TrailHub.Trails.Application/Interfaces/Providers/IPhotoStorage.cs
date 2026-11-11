namespace TrailHub.Trails.Application.Interfaces.Providers;

/// <summary>Object storage for user photos (S3 or an S3-compatible store such as MinIO).</summary>
public interface IPhotoStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Deletes a stored photo. Best effort: storage failures are logged, not thrown.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>Public URL for a stored key; external absolute URLs are returned unchanged.</summary>
    string ResolveUrl(string keyOrUrl);
}
