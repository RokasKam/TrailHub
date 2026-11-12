namespace TrailHub.Trips.Application.Responses;

/// <param name="Total">Everything spent, in the trip currency.</param>
/// <param name="Expenses">Newest date first.</param>
/// <param name="Payments">Newest first.</param>
/// <param name="Balances">Everyone on the trip, plus people who left but still have a balance.</param>
/// <param name="Suggestions">Few payments that settle everyone up.</param>
/// <param name="Participants">Who can pay, share costs and settle up now.</param>
public sealed record TripExpensesResponse(
    string Currency,
    decimal Total,
    IReadOnlyList<ExpenseResponse> Expenses,
    IReadOnlyList<PaymentResponse> Payments,
    IReadOnlyList<BalanceResponse> Balances,
    IReadOnlyList<SettlementSuggestionResponse> Suggestions,
    IReadOnlyList<PersonResponse> Participants);
