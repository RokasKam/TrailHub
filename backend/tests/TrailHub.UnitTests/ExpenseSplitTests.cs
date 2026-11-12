using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.UnitTests;

public sealed class ExpenseSplitTests
{
    [Fact]
    public void Equal_split_gives_leftover_cents_in_user_id_order()
    {
        var shares = ExpenseSplitter.Equal(10.00m, ["cleo", "anna", "ben"]).Value;

        Assert.Equal([("anna", 3.34m), ("ben", 3.33m), ("cleo", 3.33m)], shares.Select(s => (s.UserId, s.Amount)));
        Assert.Equal(10.00m, shares.Sum(s => s.Amount));
    }

    [Theory]
    [InlineData(0.01, 3)]
    [InlineData(100.00, 3)]
    [InlineData(99.99, 7)]
    [InlineData(1000000.00, 9)]
    public void Equal_shares_always_add_up(double total, int people)
    {
        var amount = (decimal)total;
        var shares = ExpenseSplitter.Equal(amount, Enumerable.Range(0, people).Select(i => $"u{i}")).Value;

        Assert.Equal(amount, shares.Sum(s => s.Amount));
        Assert.True(shares.Max(s => s.Amount) - shares.Min(s => s.Amount) <= 0.01m);
    }

    [Fact]
    public void Equal_split_ignores_duplicates_and_needs_someone()
    {
        Assert.Equal(2, ExpenseSplitter.Equal(5m, ["a", "a", "b"]).Value.Count);
        Assert.Equal(TripErrors.EmptySplit, ExpenseSplitter.Equal(5m, []).Error);
    }

    [Fact]
    public void Custom_split_must_add_up_to_the_amount()
    {
        Assert.Equal(TripErrors.SplitMismatch, ExpenseSplitter.Custom([new("a", 10m), new("b", 5m)], 16m, 1m, 16m).Error);
        Assert.Equal(TripErrors.EmptySplit, ExpenseSplitter.Custom([new("a", 0m)], 16m, 1m, 16m).Error);
        Assert.Equal(TripErrors.InvalidExpense, ExpenseSplitter.Custom([new("a", -1m), new("b", 17m)], 16m, 1m, 16m).Error);
    }

    [Fact]
    public void Custom_split_converts_and_puts_the_rounding_on_the_largest_share()
    {
        // 10.00 CHF at 1.0612: 3.33 → 3.53 (twice) and 3.34 → 3.54 make 10.60, but the whole is 10.612 → 10.61.
        var total = Money.Convert(10.00m, 1.0612m);
        var shares = ExpenseSplitter.Custom([new("a", 3.33m), new("b", 3.34m), new("c", 3.33m)], 10.00m, 1.0612m, total).Value;

        Assert.Equal(10.61m, total);
        Assert.Equal(total, shares.Sum(s => s.Amount));
    }

    [Fact]
    public void Custom_split_rounding_difference_lands_on_the_largest_share()
    {
        // Each 0.05 at 0.5 is 0.025 → 0.03 (away from zero); three of them are 0.09, but 0.15 × 0.5 = 0.075 → 0.08.
        var total = Money.Convert(0.15m, 0.5m);
        var shares = ExpenseSplitter.Custom([new("b", 0.05m), new("a", 0.05m), new("c", 0.05m)], 0.15m, 0.5m, total).Value;

        Assert.Equal(0.08m, total);
        Assert.Equal(0.08m, shares.Sum(s => s.Amount));
        Assert.Equal(0.02m, shares.Single(s => s.UserId == "a").Amount); // tie on size → first by user id takes the difference
        Assert.Equal(0.03m, shares.Single(s => s.UserId == "b").Amount);
    }

    [Fact]
    public void Conversion_rounds_half_away_from_zero()
    {
        Assert.Equal(0.03m, Money.Convert(0.05m, 0.5m));
        Assert.Equal(2.13m, Money.Convert(2.125m, 1m));
    }
}
