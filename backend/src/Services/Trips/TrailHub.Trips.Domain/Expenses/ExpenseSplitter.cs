using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Expenses;

/// <summary>Turns an expense into per-person shares in the trip currency that always add up exactly.</summary>
public static class ExpenseSplitter
{
    /// <summary>
    /// Divides <paramref name="total"/> (two decimals) evenly. The leftover cents go one each to the people first in
    /// user-id order, so €10.00 among three is 3.34 / 3.33 / 3.33.
    /// </summary>
    public static Result<IReadOnlyList<ExpenseShare>> Equal(decimal total, IEnumerable<string> participants)
    {
        var people = participants.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (people.Count == 0)
        {
            return TripErrors.EmptySplit;
        }

        var cents = (long)(total * 100);
        var each = cents / people.Count;
        var leftover = cents % people.Count;
        return people.Select((userId, i) => new ExpenseShare(userId, (each + (i < leftover ? 1 : 0)) / 100m)).ToList();
    }

    /// <summary>
    /// Custom amounts in the expense currency, which must add up to <paramref name="amount"/>. Each is converted with
    /// <paramref name="rate"/>; the rounding difference to <paramref name="amountInTripCurrency"/> goes on the largest share.
    /// People with a zero amount are left out.
    /// </summary>
    public static Result<IReadOnlyList<ExpenseShare>> Custom(IReadOnlyList<CustomShare> shares, decimal amount, decimal rate, decimal amountInTripCurrency)
    {
        if (shares.Any(s => string.IsNullOrWhiteSpace(s.UserId) || s.Amount < 0 || !Money.HasAtMostDecimals(s.Amount, 2))
            || shares.Select(s => s.UserId).Distinct(StringComparer.Ordinal).Count() != shares.Count)
        {
            return TripErrors.InvalidExpense;
        }

        var paying = shares.Where(s => s.Amount > 0).ToList();
        if (paying.Count == 0)
        {
            return TripErrors.EmptySplit;
        }

        if (paying.Sum(s => s.Amount) != amount)
        {
            return TripErrors.SplitMismatch;
        }

        var converted = paying.Select(s => new ExpenseShare(s.UserId!, Money.Convert(s.Amount, rate))).ToList();
        var difference = amountInTripCurrency - converted.Sum(s => s.Amount);
        if (difference != 0)
        {
            var largest = paying
                .Select((s, i) => (s.Amount, s.UserId, Index: i))
                .OrderByDescending(x => x.Amount)
                .ThenBy(x => x.UserId, StringComparer.Ordinal)
                .First().Index;
            converted[largest] = converted[largest] with { Amount = converted[largest].Amount + difference };
        }

        return converted;
    }
}
