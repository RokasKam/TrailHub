namespace TrailHub.Trails.Application.Responses;

/// <param name="Id">Photo id.</param>
/// <param name="Url">Where to load the photo from.</param>
/// <param name="Caption">Optional caption.</param>
/// <param name="UploadedAtUtc">When it was added.</param>
/// <param name="UploadedBy">Who added it.</param>
/// <param name="Hidden">Hidden after reports or by a moderator (only its uploader and moderators still see it).</param>
/// <param name="CanDelete">Whether the caller may delete it (they added it, wrote the trail, or moderate).</param>
public sealed record TrailPhotoResponse(Guid Id, string Url, string? Caption, DateTime UploadedAtUtc, string UploadedBy, bool Hidden, bool CanDelete);
