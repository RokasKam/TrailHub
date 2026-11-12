using TrailHub.Trips.Domain.Expenses;

namespace TrailHub.Trips.Application.Responses;

/// <param name="Amount">In <paramref name="Currency"/>.</param>
/// <param name="Rate">Trip-currency units per one <paramref name="Currency"/>; 1 for the trip currency.</param>
/// <param name="Shares">In the trip currency; they add up to <paramref name="AmountInTripCurrency"/>.</param>
/// <param name="CanEdit">Whether the caller may change or delete it.</param>
public sealed record ExpenseResponse(
    Guid Id,
    string Description,
    ExpenseCategory Category,
    PersonResponse PaidBy,
    decimal Amount,
    string Currency,
    decimal Rate,
    decimal AmountInTripCurrency,
    DateOnly Date,
    SplitMode SplitMode,
    IReadOnlyList<ExpenseShareResponse> Shares,
    string CreatedBy,
    DateTime CreatedAtUtc,
    bool CanEdit);
