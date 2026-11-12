using TrailHub.Trips.Domain.Expenses;

namespace TrailHub.UnitTests;

public sealed class LedgerBalanceTests
{
    private static readonly DateTime Now = new(2027, 7, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly July1 = new(2027, 7, 1);
    private static readonly Guid TripId = Guid.NewGuid();

    private static readonly LedgerContext Context = new("EUR", new Dictionary<string, string> { ["anna"] = "Anna", ["ben"] = "Ben", ["cleo"] = "Cleo", ["dan"] = "Dan" });

    private static TripExpense Paid(string by, decimal amount, params string[] among) =>
        TripExpense.Create(TripId, by, new("Cost", ExpenseCategory.Other, by, amount, "EUR", null, July1, SplitMode.Equal, among, null), Context, Now).Value;

    private static TripPayment Payment(string from, string to, decimal amount) =>
        TripPayment.Create(TripId, from, new(from, to, amount, July1), Context, Now).Value;

    [Fact]
    public void Balances_add_up_to_zero_and_include_everyone()
    {
        var balances = LedgerBalances.Compute([Paid("anna", 100m, "anna", "ben", "cleo"), Paid("ben", 10m, "ben", "cleo")], [], ["anna", "ben", "cleo", "dan"]);

        Assert.Equal(0m, balances.Sum(b => b.Net));
        Assert.Equal(66.66m, balances.Single(b => b.UserId == "anna").Net); // paid 100, share 33.34
        Assert.Equal(-28.33m, balances.Single(b => b.UserId == "ben").Net); // paid 10, share 33.33 + 5
        Assert.Equal(-38.33m, balances.Single(b => b.UserId == "cleo").Net);
        Assert.Equal(0m, balances.Single(b => b.UserId == "dan").Net);
    }

    [Fact]
    public void Payments_move_balances_toward_zero()
    {
        var balances = LedgerBalances.Compute([Paid("anna", 30m, "anna", "ben", "cleo")], [Payment("ben", "anna", 10m), Payment("cleo", "anna", 10m)], ["anna", "ben", "cleo"]);

        Assert.All(balances, b => Assert.Equal(0m, b.Net));
        Assert.Equal(10m, balances.Single(b => b.UserId == "ben").Sent);
        Assert.Equal(20m, balances.Single(b => b.UserId == "anna").Received);
    }

    [Fact]
    public void People_who_left_keep_their_balance()
    {
        var balances = LedgerBalances.Compute([Paid("dan", 20m, "anna", "dan")], [], ["anna"]);

        Assert.Equal(10m, balances.Single(b => b.UserId == "dan").Net);
        Assert.Equal(-10m, balances.Single(b => b.UserId == "anna").Net);
    }
}
