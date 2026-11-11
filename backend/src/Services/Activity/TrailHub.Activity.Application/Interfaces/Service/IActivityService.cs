using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Activity.Application.Interfaces.Service;

/// <summary>The caller's recorded and imported hikes, GPX export and hiking statistics.</summary>
public interface IActivityService
{
    Task<PagedResult<ActivitySummaryResponse>> GetActivitiesAsync(PageRequest page, CancellationToken cancellationToken);

    Task<ActivityDetailsResponse> GetActivityAsync(Guid id, CancellationToken cancellationToken);

    Task<GpxFileResponse> ExportGpxAsync(Guid id, CancellationToken cancellationToken);

    Task<HikingStatisticsResponse> GetStatisticsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Idempotent upload of a finished recording. Re-sending the same id (e.g. an offline queue
    /// retrying after a timeout) returns the stored activity instead of creating a duplicate.
    /// </summary>
    Task<ActivitySummaryResponse> RecordAsync(Guid id, RecordActivityRequest request, CancellationToken cancellationToken);

    Task<ActivitySummaryResponse> ImportGpxAsync(Stream gpx, string? name, Guid? trailId, CancellationToken cancellationToken);

    Task UpdateAsync(Guid id, UpdateActivityRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
