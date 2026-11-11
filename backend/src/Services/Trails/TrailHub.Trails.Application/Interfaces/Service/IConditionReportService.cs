using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>Trail condition reports (mud, snow, closures…).</summary>
public interface IConditionReportService
{
    /// <summary>Recent reports, newest first; <paramref name="take"/> is clamped to 1–100.</summary>
    Task<IReadOnlyList<ConditionReportResponse>> GetRecentAsync(Guid trailId, int take, CancellationToken cancellationToken);

    Task<ConditionReportResponse> ReportAsync(Guid trailId, ConditionReportRequest request, CancellationToken cancellationToken);
}
