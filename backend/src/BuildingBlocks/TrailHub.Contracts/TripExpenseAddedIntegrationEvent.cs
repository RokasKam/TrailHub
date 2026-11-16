namespace TrailHub.Contracts;

/// <summary>An expense was logged on a trip. <paramref name="Currency"/> is the trip currency, which the shares are in.</summary>
public sealed record TripExpenseAddedIntegrationEvent(
    Guid TripId,
    string TripName,
    Guid ExpenseId,
    string Description,
    string CreatedById,
    string CreatedByName,
    string Currency,
    IReadOnlyList<TripExpenseShare> Shares);
