namespace TrailHub.Activity.Application.Responses;

/// <summary>The author of a post or comment, named as in the group membership.</summary>
public sealed record FeedUserResponse(string UserId, string DisplayName);
