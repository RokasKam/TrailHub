namespace TrailHub.Trips.Domain.Expenses;

/// <summary>What someone entered for an expense, before validation.</summary>
/// <param name="Rate">Trip-currency units per one unit of <paramref name="Currency"/>; needed only for a foreign currency.</param>
/// <param name="Participants">Equal split: who shares the cost.</param>
/// <param name="CustomShares">Custom split: each person's amount in the expense currency.</param>
public sealed record ExpenseDetails(
    string? Description,
    ExpenseCategory Category,
    string? PaidBy,
    decimal Amount,
    string? Currency,
    decimal? Rate,
    DateOnly Date,
    SplitMode SplitMode,
    IReadOnlyList<string>? Participants,
    IReadOnlyList<CustomShare>? CustomShares);
