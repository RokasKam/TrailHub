namespace TrailHub.Trails.Application.Responses;

public sealed record ReviewResponse(Guid Id, string UserId, int Rating, string? Comment, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
