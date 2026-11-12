using TrailHub.Trips.Domain.Expenses;

namespace TrailHub.Trips.Application.Requests;

/// <param name="Amount">In <paramref name="Currency"/>, at most two decimals.</param>
/// <param name="Rate">Trip-currency units per one <paramref name="Currency"/>; required when it isn't the trip currency, ignored otherwise.</param>
/// <param name="Category">Defaults to <c>Other</c>.</param>
/// <param name="SplitMode">Defaults to <c>Equal</c>.</param>
/// <param name="Participants">Equal split: who shares it (user ids).</param>
/// <param name="CustomShares">Custom split: each person's amount in <paramref name="Currency"/>, adding up to <paramref name="Amount"/>.</param>
public sealed record ExpenseRequest(
    string? Description,
    ExpenseCategory? Category,
    string? PaidBy,
    decimal Amount,
    string? Currency,
    decimal? Rate,
    DateOnly Date,
    SplitMode? SplitMode,
    IReadOnlyList<string>? Participants,
    IReadOnlyList<CustomShareRequest>? CustomShares);
