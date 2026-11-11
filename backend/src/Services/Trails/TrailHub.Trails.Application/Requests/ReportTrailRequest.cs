using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Requests;

public sealed record ReportTrailRequest(ReportReason Reason, string? Details);
