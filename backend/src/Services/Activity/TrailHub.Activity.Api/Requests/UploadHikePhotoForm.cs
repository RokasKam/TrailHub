namespace TrailHub.Activity.Api.Requests;

/// <summary>Multipart form for <c>POST /api/activities/{id}/photos</c>.</summary>
public sealed record UploadHikePhotoForm
{
    /// <summary>A JPEG, PNG, HEIC or WebP image of at most 10 MB.</summary>
    public required IFormFile File { get; init; }

    public string? Caption { get; init; }
}
