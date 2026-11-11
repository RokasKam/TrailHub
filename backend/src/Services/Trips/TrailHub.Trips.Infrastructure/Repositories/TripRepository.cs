using Microsoft.EntityFrameworkCore;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Trips.Infrastructure.Persistence;

namespace TrailHub.Trips.Infrastructure.Repositories;

internal sealed class TripRepository(TripsDbContext db) : ITripRepository
{
    public Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Trips.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetVisibleToAsync(string userId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken) =>
        await db.Trips.AsNoTracking()
            .Where(t => t.OwnerId == userId || (t.GroupId != null && groupIds.Contains(t.GroupId.Value)))
            .OrderBy(t => t.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetByGroupAsync(Guid groupId, CancellationToken cancellationToken) =>
        await db.Trips.Where(t => t.GroupId == groupId).OrderBy(t => t.StartDate).ToListAsync(cancellationToken);

    public void Add(Trip trip) => db.Trips.Add(trip);

    public void Remove(Trip trip) => db.Trips.Remove(trip);
}
