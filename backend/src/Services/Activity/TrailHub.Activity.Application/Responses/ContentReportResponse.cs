using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Application.Responses;

public sealed record ContentReportResponse(string UserId, ContentReportReason Reason, string? Details, DateTime CreatedAtUtc);
