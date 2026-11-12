using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.UnitTests;

public sealed class TripExpenseTests
{
    private static readonly DateTime Now = new(2027, 7, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly July1 = new(2027, 7, 1);
    private static readonly Guid TripId = Guid.NewGuid();

    private static readonly LedgerContext Euro = new("EUR", new Dictionary<string, string> { ["anna"] = "Anna", ["ben"] = "Ben", ["cleo"] = "Cleo" });

    private static ExpenseDetails Dinner(decimal amount = 30m, string currency = "EUR", decimal? rate = null, string paidBy = "anna", IReadOnlyList<string>? participants = null) =>
        new("Dinner", ExpenseCategory.Food, paidBy, amount, currency, rate, July1, SplitMode.Equal, participants ?? ["anna", "ben", "cleo"], null);

    [Fact]
    public void Same_currency_has_rate_one()
    {
        var expense = TripExpense.Create(TripId, "anna", Dinner(rate: 2m), Euro, Now).Value;

        Assert.Equal(1m, expense.Rate);
        Assert.Equal(30m, expense.AmountInTripCurrency);
        Assert.All(expense.Shares, s => Assert.Equal(10m, s.Amount));
    }

    [Fact]
    public void Foreign_currency_needs_a_valid_rate_and_is_converted()
    {
        Assert.Equal(TripErrors.RateRequired, TripExpense.Create(TripId, "anna", Dinner(currency: "CHF"), Euro, Now).Error);
        Assert.Equal(TripErrors.InvalidRate, TripExpense.Create(TripId, "anna", Dinner(currency: "CHF", rate: 0m), Euro, Now).Error);
        Assert.Equal(TripErrors.InvalidRate, TripExpense.Create(TripId, "anna", Dinner(currency: "CHF", rate: 1.0000001m), Euro, Now).Error);

        var expense = TripExpense.Create(TripId, "anna", Dinner(amount: 100m, currency: "chf", rate: 1.0612m), Euro, Now).Value;
        Assert.Equal("CHF", expense.Currency);
        Assert.Equal(106.12m, expense.AmountInTripCurrency);
        Assert.Equal(106.12m, expense.Shares.Sum(s => s.Amount));
    }

    [Fact]
    public void Validates_description_amount_currency_and_people()
    {
        Assert.Equal(TripErrors.InvalidExpense, TripExpense.Create(TripId, "anna", Dinner() with { Description = " " }, Euro, Now).Error);
        Assert.Equal(TripErrors.InvalidExpense, TripExpense.Create(TripId, "anna", Dinner(amount: 0m), Euro, Now).Error);
        Assert.Equal(TripErrors.InvalidExpense, TripExpense.Create(TripId, "anna", Dinner(amount: 1.005m), Euro, Now).Error);
        Assert.Equal(TripErrors.InvalidExpense, TripExpense.Create(TripId, "anna", Dinner(amount: 1_000_000.01m), Euro, Now).Error);
        Assert.Equal(TripErrors.InvalidCurrency, TripExpense.Create(TripId, "anna", Dinner(currency: "EURO"), Euro, Now).Error);
        Assert.Equal(TripErrors.NotAParticipant, TripExpense.Create(TripId, "anna", Dinner(paidBy: "zed"), Euro, Now).Error);
        Assert.Equal(TripErrors.NotAParticipant, TripExpense.Create(TripId, "anna", Dinner(participants: ["anna", "zed"]), Euro, Now).Error);
        Assert.Equal(TripErrors.EmptySplit, TripExpense.Create(TripId, "anna", Dinner(participants: []), Euro, Now).Error);
    }

    [Fact]
    public void Custom_split_is_in_the_expense_currency()
    {
        var details = Dinner(amount: 50m, currency: "CHF", rate: 1.1m) with
        {
            SplitMode = SplitMode.Custom,
            CustomShares = [new("anna", 20m), new("ben", 30m)],
        };

        var expense = TripExpense.Create(TripId, "anna", details, Euro, Now).Value;

        Assert.Equal([("anna", 22m), ("ben", 33m)], expense.Shares.Select(s => (s.UserId, s.Amount)));
        Assert.Equal(TripErrors.SplitMismatch, TripExpense.Create(TripId, "anna", details with { CustomShares = [new("anna", 20m)] }, Euro, Now).Error);
    }

    [Fact]
    public void Creator_payer_and_owner_can_edit_others_cannot()
    {
        var expense = TripExpense.Create(TripId, "ben", Dinner(paidBy: "anna"), Euro, Now).Value;

        Assert.True(expense.CanEdit("ben", "owner"));
        Assert.True(expense.CanEdit("anna", "owner"));
        Assert.True(expense.CanEdit("owner", "owner"));
        Assert.False(expense.CanEdit("cleo", "owner"));
        Assert.Equal(TripErrors.NotExpenseEditor, expense.Update("cleo", "owner", Dinner(amount: 1m), Euro, Now).Error);
        Assert.True(expense.Update("anna", "owner", Dinner(amount: 12m), Euro, Now).IsSuccess);
        Assert.Equal(12m, expense.AmountInTripCurrency);
    }

    [Fact]
    public void Expenses_remember_the_names_of_the_people_in_them()
    {
        var expense = TripExpense.Create(TripId, "anna", Dinner(participants: ["ben"]), Euro, Now).Value;

        Assert.Equal([("anna", "Anna"), ("ben", "Ben")], expense.KnownNames.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)));
    }

    [Fact]
    public void Payments_need_two_different_participants_and_a_positive_amount()
    {
        Assert.Equal(TripErrors.InvalidPayment, TripPayment.Create(TripId, "anna", new("anna", "anna", 5m, July1), Euro, Now).Error);
        Assert.Equal(TripErrors.InvalidPayment, TripPayment.Create(TripId, "anna", new("anna", "ben", 0m, July1), Euro, Now).Error);
        Assert.Equal(TripErrors.NotAParticipant, TripPayment.Create(TripId, "anna", new("anna", "zed", 5m, July1), Euro, Now).Error);

        var payment = TripPayment.Create(TripId, "cleo", new("ben", "anna", 5m, July1), Euro, Now).Value;
        Assert.True(payment.CanDelete("cleo", "owner") && payment.CanDelete("ben", "owner") && payment.CanDelete("anna", "owner") && payment.CanDelete("owner", "owner"));
        Assert.False(payment.CanDelete("dan", "owner"));
    }

    [Fact]
    public void Trip_currency_defaults_to_euro_and_locks_once_something_is_logged()
    {
        var trip = Trip.Create("anna", "Haute Route", null, July1, July1.AddDays(5), Now).Value;

        Assert.Equal("EUR", trip.Currency);
        Assert.True(trip.SetCurrency("chf", ledgerIsEmpty: true).IsSuccess);
        Assert.Equal("CHF", trip.Currency);
        Assert.Equal(TripErrors.CurrencyLocked, trip.SetCurrency("EUR", ledgerIsEmpty: false).Error);
        Assert.True(trip.SetCurrency("CHF", ledgerIsEmpty: false).IsSuccess);
        Assert.Equal(TripErrors.InvalidCurrency, trip.SetCurrency("francs", ledgerIsEmpty: true).Error);
    }
}
