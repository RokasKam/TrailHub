using TrailHub.Routes.Application.Responses;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Interfaces.Repository;

public interface IRouteRepository
{
    Task<PlannedRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The user's own routes and routes shared with any of <paramref name="groupIds"/>.</summary>
    Task<IReadOnlyList<RouteSummaryResponse>> GetSummariesVisibleToAsync(string userId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken);

    void Add(PlannedRoute route);

    void Remove(PlannedRoute route);
}
