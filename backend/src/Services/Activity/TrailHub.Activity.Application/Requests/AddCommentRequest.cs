namespace TrailHub.Activity.Application.Requests;

/// <param name="Text">1 to 1000 characters after trimming.</param>
public sealed record AddCommentRequest(string? Text);
