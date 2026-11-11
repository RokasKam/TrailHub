namespace TrailHub.Trails.Api.Requests;

/// <summary>Multipart form for <c>POST /api/trails/{id}/photos/upload</c>.</summary>
public sealed record UploadPhotoForm
{
    public required IFormFile File { get; init; }

    public string? Caption { get; init; }
}
