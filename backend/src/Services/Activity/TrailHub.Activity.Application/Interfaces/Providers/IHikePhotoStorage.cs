namespace TrailHub.Activity.Application.Interfaces.Providers;

/// <summary>Object storage for hike photos (the same S3-compatible store as trail photos, keys under <c>hikes/</c>).</summary>
public interface IHikePhotoStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Deletes a stored photo. Best effort: storage failures are logged, not thrown.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>Public URL for a stored key.</summary>
    string ResolveUrl(string key);
}
