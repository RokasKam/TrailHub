using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Interfaces.Repository;

public interface ITrailRepository
{
    Task<Trail?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    void Add(Trail trail);

    /// <summary>Read-side projection: returns summaries without loading full routes.</summary>
    Task<PagedResult<TrailSummaryResponse>> SearchAsync(TrailSearchCriteria criteria, PageRequest page, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrailSummaryResponse>> GetSummariesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
