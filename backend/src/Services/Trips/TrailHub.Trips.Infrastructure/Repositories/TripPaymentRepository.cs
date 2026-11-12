using Microsoft.EntityFrameworkCore;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Infrastructure.Persistence;

namespace TrailHub.Trips.Infrastructure.Repositories;

internal sealed class TripPaymentRepository(TripsDbContext db) : ITripPaymentRepository
{
    public Task<TripPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.TripPayments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TripPayment>> GetByTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        await db.TripPayments.AsNoTracking().Where(p => p.TripId == tripId).ToListAsync(cancellationToken);

    public Task<bool> AnyForTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        db.TripPayments.AnyAsync(p => p.TripId == tripId, cancellationToken);

    public void Add(TripPayment payment) => db.TripPayments.Add(payment);

    public void Remove(TripPayment payment) => db.TripPayments.Remove(payment);
}
