using TrailHub.Trips.Domain.Expenses;

namespace TrailHub.Trips.Application.Interfaces.Repository;

public interface ITripPaymentRepository
{
    Task<TripPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TripPayment>> GetByTripAsync(Guid tripId, CancellationToken cancellationToken);

    Task<bool> AnyForTripAsync(Guid tripId, CancellationToken cancellationToken);

    void Add(TripPayment payment);

    void Remove(TripPayment payment);
}
