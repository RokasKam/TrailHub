using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Requests;

public sealed record ConditionReportRequest(TrailCondition Condition, string? Note);
