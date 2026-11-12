namespace TrailHub.Trips.Domain.Expenses;

/// <summary>
/// What an expense or payment is checked against: the trip's currency and its current participants
/// (user id → display name). Names are remembered with each entry so people who leave keep a name.
/// </summary>
public sealed record LedgerContext(string TripCurrency, IReadOnlyDictionary<string, string> Participants);
