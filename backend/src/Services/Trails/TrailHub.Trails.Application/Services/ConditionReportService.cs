using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Mappings;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class ConditionReportService(
    ITrailRepository trails,
    IConditionReportRepository reports,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IConditionReportService
{
    public async Task<IReadOnlyList<ConditionReportResponse>> GetRecentAsync(Guid trailId, int take, CancellationToken cancellationToken)
    {
        var recent = await reports.GetRecentAsync(trailId, Math.Clamp(take, 1, 100), cancellationToken);
        return recent.Select(r => r.ToResponse()).ToList();
    }

    public async Task<ConditionReportResponse> ReportAsync(Guid trailId, ConditionReportRequest request, CancellationToken cancellationToken)
    {
        if (!await trails.ExistsAsync(trailId, cancellationToken))
        {
            throw TrailErrors.NotFound.ToException();
        }

        var report = ConditionReport.Create(trailId, user.UserId, request.Condition, request.Note, clock.GetUtcNow().UtcDateTime).GetValueOrThrow();
        reports.Add(report);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return report.ToResponse();
    }
}
