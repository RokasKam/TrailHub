namespace TrailHub.Trips.Application.Responses;

/// <param name="Total">Everything spent, in the trip currency.</param>
/// <param name="MyNet">The caller's balance: above zero means others owe them.</param>
public sealed record ExpenseSummaryResponse(decimal Total, decimal MyNet, int ExpenseCount);
