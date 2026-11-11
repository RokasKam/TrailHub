using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Responses;

public sealed record ConditionReportResponse(Guid Id, TrailCondition Condition, string? Note, string UserId, DateTime ReportedAtUtc);
