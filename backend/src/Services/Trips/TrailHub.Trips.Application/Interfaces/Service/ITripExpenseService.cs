using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;

namespace TrailHub.Trips.Application.Interfaces.Service;

/// <summary>
/// A trip's shared costs: expenses split among participants, settle-up payments, balances and suggested payments.
/// Open to everyone who can see the trip, in any trip status. Every change tells people watching the trip.
/// </summary>
public interface ITripExpenseService
{
    /// <summary>Expenses, payments, balances, settle-up suggestions and the people who can take part.</summary>
    Task<TripExpensesResponse> GetAsync(Guid tripId, CancellationToken cancellationToken);

    Task<ExpenseResponse> AddExpenseAsync(Guid tripId, ExpenseRequest request, CancellationToken cancellationToken);

    /// <summary>Replaces an expense. Allowed for whoever added or paid it, and the trip owner.</summary>
    Task<ExpenseResponse> UpdateExpenseAsync(Guid tripId, Guid expenseId, ExpenseRequest request, CancellationToken cancellationToken);

    Task DeleteExpenseAsync(Guid tripId, Guid expenseId, CancellationToken cancellationToken);

    Task<PaymentResponse> RecordPaymentAsync(Guid tripId, PaymentRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes a payment. Allowed for whoever recorded it, the two people in it, and the trip owner.</summary>
    Task DeletePaymentAsync(Guid tripId, Guid paymentId, CancellationToken cancellationToken);
}
