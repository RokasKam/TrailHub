using TrailHub.Trips.Domain.Expenses;

namespace TrailHub.Trips.Application.Interfaces.Repository;

public interface ITripExpenseRepository
{
    Task<TripExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TripExpense>> GetByTripAsync(Guid tripId, CancellationToken cancellationToken);

    Task<bool> AnyForTripAsync(Guid tripId, CancellationToken cancellationToken);

    void Add(TripExpense expense);

    void Remove(TripExpense expense);
}
