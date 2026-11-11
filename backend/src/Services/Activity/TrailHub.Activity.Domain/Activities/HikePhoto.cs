using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Domain.Activities;

/// <param name="Id">Photo id.</param>
/// <param name="Key">Key in TrailHub's photo storage (under <c>hikes/</c>), resolved to a URL when read.</param>
/// <param name="Caption">Optional caption.</param>
/// <param name="AddedAtUtc">When it was uploaded.</param>
/// <param name="HiddenBy">Set while the photo is hidden after reports or by a moderator.</param>
public sealed record HikePhoto(Guid Id, string Key, string? Caption, DateTime AddedAtUtc, HiddenBy? HiddenBy = null)
{
    // Methods rather than properties: the photo is stored as JSON and computed properties would be stored too.

    /// <summary>Hidden photos exist only for the hike's author and moderators.</summary>
    public bool IsVisibleTo(string userId, string hikeAuthorId, bool isModerator) =>
        HiddenBy is null || userId == hikeAuthorId || isModerator;
}
