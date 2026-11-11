namespace TrailHub.Activity.Application.Responses;

/// <summary>A group a post is shared with (only groups the caller is in are listed).</summary>
public sealed record FeedGroupResponse(Guid GroupId, string Name);
