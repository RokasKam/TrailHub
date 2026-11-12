using Microsoft.EntityFrameworkCore;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Infrastructure.Persistence;

namespace TrailHub.Trips.Infrastructure.Repositories;

internal sealed class TripExpenseRepository(TripsDbContext db) : ITripExpenseRepository
{
    public Task<TripExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.TripExpenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TripExpense>> GetByTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        await db.TripExpenses.AsNoTracking().Where(e => e.TripId == tripId).ToListAsync(cancellationToken);

    public Task<bool> AnyForTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        db.TripExpenses.AnyAsync(e => e.TripId == tripId, cancellationToken);

    public void Add(TripExpense expense) => db.TripExpenses.Add(expense);

    public void Remove(TripExpense expense) => db.TripExpenses.Remove(expense);
}
