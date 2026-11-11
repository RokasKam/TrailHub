using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Application.Responses;

/// <summary>A comment or hike photo with open reports, or hidden, in the moderators' queue.</summary>
/// <param name="Kind">Comment or HikePhoto.</param>
/// <param name="ActivityId">The hike it belongs to.</param>
/// <param name="ContentId">The comment's or photo's id.</param>
/// <param name="Text">The comment (null for photos).</param>
/// <param name="Url">The photo (null for comments).</param>
/// <param name="AuthorId">Who wrote the comment or posted the photo.</param>
/// <param name="AuthorName">Their name as their groups know them.</param>
/// <param name="HiddenBy">Null while it's still shown.</param>
/// <param name="OpenReports">Reports not yet resolved by a moderator.</param>
/// <param name="Reports">The open reports, newest first.</param>
public sealed record ContentModerationItemResponse(
    ContentKind Kind,
    Guid ActivityId,
    Guid ContentId,
    string? Text,
    string? Url,
    string AuthorId,
    string AuthorName,
    HiddenBy? HiddenBy,
    int OpenReports,
    IReadOnlyList<ContentReportResponse> Reports);
