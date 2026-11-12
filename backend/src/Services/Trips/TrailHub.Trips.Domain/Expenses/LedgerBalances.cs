namespace TrailHub.Trips.Domain.Expenses;

/// <summary>Adds up a trip's expenses and payments per person.</summary>
public static class LedgerBalances
{
    /// <summary>
    /// One balance for each of <paramref name="participants"/> and for anyone else who appears in the ledger (people who
    /// left keep theirs). The nets always add up to zero.
    /// </summary>
    public static IReadOnlyList<ParticipantBalance> Compute(IEnumerable<TripExpense> expenses, IEnumerable<TripPayment> payments, IEnumerable<string> participants)
    {
        var paid = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var share = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var sent = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var received = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var people = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Track(string userId)
        {
            if (seen.Add(userId))
            {
                people.Add(userId);
            }
        }

        static void Add(Dictionary<string, decimal> totals, string userId, decimal amount) =>
            totals[userId] = totals.GetValueOrDefault(userId) + amount;

        foreach (var userId in participants)
        {
            Track(userId);
        }

        foreach (var expense in expenses)
        {
            Track(expense.PaidBy);
            Add(paid, expense.PaidBy, expense.AmountInTripCurrency);
            foreach (var s in expense.Shares)
            {
                Track(s.UserId);
                Add(share, s.UserId, s.Amount);
            }
        }

        foreach (var payment in payments)
        {
            Track(payment.From);
            Track(payment.To);
            Add(sent, payment.From, payment.Amount);
            Add(received, payment.To, payment.Amount);
        }

        return people
            .Select(u => new ParticipantBalance(u, paid.GetValueOrDefault(u), share.GetValueOrDefault(u), sent.GetValueOrDefault(u), received.GetValueOrDefault(u)))
            .ToList();
    }
}
