namespace TrailHub.Activity.Application.Responses;

/// <param name="Id">Photo id.</param>
/// <param name="Url">Where to load the photo from.</param>
/// <param name="Caption">Optional caption.</param>
/// <param name="AddedAtUtc">When it was uploaded.</param>
/// <param name="Hidden">Hidden after reports or by a moderator; only the hike's author and moderators still see it.</param>
/// <param name="CanReport">Whether the caller may report it (anyone but the hike's author).</param>
/// <param name="CanDelete">Whether the caller may delete it (the hike's author or a moderator).</param>
public sealed record HikePhotoResponse(Guid Id, string Url, string? Caption, DateTime AddedAtUtc, bool Hidden, bool CanReport, bool CanDelete);
