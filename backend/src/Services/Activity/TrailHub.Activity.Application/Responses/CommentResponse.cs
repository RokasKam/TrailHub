namespace TrailHub.Activity.Application.Responses;

/// <param name="Id">Comment id.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="Text">The comment.</param>
/// <param name="CreatedAtUtc">When it was written.</param>
/// <param name="CanDelete">Whether the caller may delete it (they wrote it, they wrote the hike, or they moderate).</param>
/// <param name="CanReport">Whether the caller may report it (anyone but its writer).</param>
/// <param name="Hidden">Hidden after reports or by a moderator; only its writer and moderators still see it.</param>
public sealed record CommentResponse(Guid Id, FeedUserResponse Author, string Text, DateTime CreatedAtUtc, bool CanDelete, bool CanReport, bool Hidden);
