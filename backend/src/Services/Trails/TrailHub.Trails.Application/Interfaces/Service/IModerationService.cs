using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>Trail reports and the moderator queue.</summary>
public interface IModerationService
{
    /// <summary>Files (or updates) the caller's report. The third distinct open report takes the trail out of Discover.</summary>
    Task ReportAsync(Guid trailId, ReportTrailRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken);

    /// <summary>A moderator hides a trail or puts it back in Discover. Either way, its open reports are resolved.</summary>
    Task ModerateAsync(Guid trailId, ModerateTrailRequest request, CancellationToken cancellationToken);
}
