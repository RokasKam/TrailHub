using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Responses;

public sealed record PhotoReportResponse(string UserId, ContentReportReason Reason, string? Details, DateTime CreatedAtUtc);
