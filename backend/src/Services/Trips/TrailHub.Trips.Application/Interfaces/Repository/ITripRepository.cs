using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Interfaces.Repository;

public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Trips the user owns plus trips shared with any of <paramref name="groupIds"/>.</summary>
    Task<IReadOnlyList<Trip>> GetVisibleToAsync(string userId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Trip>> GetByGroupAsync(Guid groupId, CancellationToken cancellationToken);

    void Add(Trip trip);

    void Remove(Trip trip);
}
