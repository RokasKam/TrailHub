namespace TrailHub.Trails.Application.Requests;

/// <param name="Rating">1–5 stars.</param>
/// <param name="Comment">Optional, up to 4000 characters.</param>
public sealed record ReviewRequest(int Rating, string? Comment);
