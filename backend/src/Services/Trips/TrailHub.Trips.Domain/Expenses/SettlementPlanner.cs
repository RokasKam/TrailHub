namespace TrailHub.Trips.Domain.Expenses;

/// <summary>Suggests few payments that settle everyone up.</summary>
public static class SettlementPlanner
{
    /// <summary>
    /// Greedy minimal cash flow: the person who owes most pays the person owed most, as much as either needs,
    /// until everyone is within a cent. Ties go by user id so the suggestions don't shuffle between reloads.
    /// </summary>
    public static IReadOnlyList<Settlement> Suggest(IEnumerable<ParticipantBalance> balances)
    {
        var debtors = balances.Where(b => b.Net < 0).ToDictionary(b => b.UserId, b => -b.Net, StringComparer.Ordinal);
        var creditors = balances.Where(b => b.Net > 0).ToDictionary(b => b.UserId, b => b.Net, StringComparer.Ordinal);
        var settlements = new List<Settlement>();

        while (Largest(debtors) is { } debtor && Largest(creditors) is { } creditor)
        {
            var amount = Math.Min(debtors[debtor], creditors[creditor]);
            if (amount < 0.01m)
            {
                break;
            }

            settlements.Add(new Settlement(debtor, creditor, amount));
            Reduce(debtors, debtor, amount);
            Reduce(creditors, creditor, amount);
        }

        return settlements;
    }

    private static string? Largest(Dictionary<string, decimal> amounts) =>
        amounts.Count == 0
            ? null
            : amounts.OrderByDescending(a => a.Value).ThenBy(a => a.Key, StringComparer.Ordinal).First().Key;

    private static void Reduce(Dictionary<string, decimal> amounts, string userId, decimal by)
    {
        amounts[userId] -= by;
        if (amounts[userId] < 0.01m)
        {
            amounts.Remove(userId);
        }
    }
}
