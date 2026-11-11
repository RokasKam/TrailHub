using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>Trail discovery, details, route and elevation data, GPX import/export and editing.</summary>
public interface ITrailService
{
    /// <summary>Public trails matching the criteria (hidden trails are out of Discover).</summary>
    Task<PagedResult<TrailSummaryResponse>> SearchAsync(TrailSearchCriteria criteria, PageRequest page, CancellationToken cancellationToken);

    /// <summary>A trail with its points of interest, photos and latest condition report.</summary>
    Task<TrailDetailsResponse> GetTrailAsync(Guid trailId, CancellationToken cancellationToken);

    /// <param name="trailId">Trail id.</param>
    /// <param name="maxPoints">Optional cap for map previews; full resolution when omitted.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<TrailRouteResponse> GetRouteAsync(Guid trailId, int? maxPoints, CancellationToken cancellationToken);

    Task<IReadOnlyList<ElevationSample>> GetElevationProfileAsync(Guid trailId, int samples, CancellationToken cancellationToken);

    Task<GpxFileResponse> ExportGpxAsync(Guid trailId, CancellationToken cancellationToken);

    /// <summary>Creates a trail from a GPX file. Name/description fall back to the file's metadata; waypoints become points of interest.</summary>
    Task<CreatedTrailResponse> ImportGpxAsync(Stream gpx, string? name, string? description, string region, Difficulty difficulty, CancellationToken cancellationToken);

    Task UpdateAsync(Guid trailId, UpdateTrailRequest request, CancellationToken cancellationToken);
}
