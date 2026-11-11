using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Responses;

public sealed record ModerationReportResponse(ReportReason Reason, string? Details, DateTime CreatedAtUtc);
