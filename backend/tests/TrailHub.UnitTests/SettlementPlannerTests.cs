using TrailHub.Trips.Domain.Expenses;

namespace TrailHub.UnitTests;

public sealed class SettlementPlannerTests
{
    private static ParticipantBalance Net(string userId, decimal net) => net >= 0 ? new(userId, net, 0, 0, 0) : new(userId, 0, -net, 0, 0);

    [Fact]
    public void Largest_debtor_pays_largest_creditor_first()
    {
        var suggestions = SettlementPlanner.Suggest([Net("anna", 66.66m), Net("ben", -28.33m), Net("cleo", -38.33m)]);

        Assert.Equal([("cleo", "anna", 38.33m), ("ben", "anna", 28.33m)], suggestions.Select(s => (s.From, s.To, s.Amount)));
    }

    [Fact]
    public void Settles_everyone_with_few_payments()
    {
        var balances = new[] { Net("a", 40m), Net("b", 10m), Net("c", -25m), Net("d", -25m) };

        var suggestions = SettlementPlanner.Suggest(balances);

        Assert.Equal(3, suggestions.Count);
        Assert.All(suggestions, s => Assert.True(s.Amount >= 0.01m));
        foreach (var b in balances)
        {
            var after = b.Net + suggestions.Where(s => s.From == b.UserId).Sum(s => s.Amount) - suggestions.Where(s => s.To == b.UserId).Sum(s => s.Amount);
            Assert.Equal(0m, after);
        }
    }

    [Fact]
    public void Nothing_to_suggest_when_settled()
    {
        Assert.Empty(SettlementPlanner.Suggest([Net("a", 0m), Net("b", 0m)]));
        Assert.Empty(SettlementPlanner.Suggest([]));
    }

    [Fact]
    public void Ties_are_broken_by_user_id()
    {
        var suggestions = SettlementPlanner.Suggest([Net("z", 10m), Net("y", 10m), Net("b", -10m), Net("a", -10m)]);

        Assert.Equal([("a", "y", 10m), ("b", "z", 10m)], suggestions.Select(s => (s.From, s.To, s.Amount)));
    }
}
