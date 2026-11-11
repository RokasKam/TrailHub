namespace TrailHub.Activity.Application.Responses;

/// <param name="Id">Comment id.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="Text">The comment.</param>
/// <param name="CreatedAtUtc">When it was written.</param>
/// <param name="CanDelete">Whether the caller may delete it (they wrote it, or they wrote the hike).</param>
public sealed record CommentResponse(Guid Id, FeedUserResponse Author, string Text, DateTime CreatedAtUtc, bool CanDelete);
