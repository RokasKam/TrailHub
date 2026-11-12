namespace TrailHub.Trips.Application.Responses;

/// <param name="Paid">Expenses they paid.</param>
/// <param name="Share">Their part of all expenses.</param>
/// <param name="Net">Paid − share + payments sent − payments received; above zero means others owe them.</param>
public sealed record BalanceResponse(string UserId, string DisplayName, decimal Paid, decimal Share, decimal Net, bool IsMe);
