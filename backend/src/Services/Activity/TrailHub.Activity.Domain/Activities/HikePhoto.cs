namespace TrailHub.Activity.Domain.Activities;

/// <param name="Id">Photo id.</param>
/// <param name="Key">Key in TrailHub's photo storage (under <c>hikes/</c>), resolved to a URL when read.</param>
/// <param name="Caption">Optional caption.</param>
/// <param name="AddedAtUtc">When it was uploaded.</param>
public sealed record HikePhoto(Guid Id, string Key, string? Caption, DateTime AddedAtUtc);
