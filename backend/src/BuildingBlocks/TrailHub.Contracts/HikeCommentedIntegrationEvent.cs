namespace TrailHub.Contracts;

/// <summary>Someone commented on a shared hike. <paramref name="Excerpt"/> is the start of the comment (at most 120 characters).</summary>
public sealed record HikeCommentedIntegrationEvent(
    Guid ActivityId,
    string OwnerId,
    string HikeName,
    Guid CommentId,
    string CommenterId,
    string CommenterName,
    string Excerpt);
