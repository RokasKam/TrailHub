using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Application.Interfaces.Repository;

public interface IGearListRepository
{
    Task<GearList?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<GearList>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    Task<int> CountByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    void Add(GearList list);

    void Remove(GearList list);
}
