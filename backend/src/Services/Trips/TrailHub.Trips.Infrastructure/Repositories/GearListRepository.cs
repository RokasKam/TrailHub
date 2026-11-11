using Microsoft.EntityFrameworkCore;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Domain.Gear;
using TrailHub.Trips.Infrastructure.Persistence;

namespace TrailHub.Trips.Infrastructure.Repositories;

internal sealed class GearListRepository(TripsDbContext db) : IGearListRepository
{
    public Task<GearList?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => db.GearLists.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GearList>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        await db.GearLists.AsNoTracking().Where(l => l.OwnerId == ownerId).OrderByDescending(l => l.UpdatedAtUtc).ToListAsync(cancellationToken);

    public Task<int> CountByOwnerAsync(string ownerId, CancellationToken cancellationToken) => db.GearLists.CountAsync(l => l.OwnerId == ownerId, cancellationToken);

    public void Add(GearList list) => db.GearLists.Add(list);

    public void Remove(GearList list) => db.GearLists.Remove(list);
}
