using TrailHub.Trips.Application.Models;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Expenses;

namespace TrailHub.Trips.Application.Mappings;

internal static class ExpenseMappings
{
    public static PersonResponse Person(this TripPeople people, string userId) => new(userId, people.NameOf(userId));

    public static ExpenseResponse ToResponse(this TripExpense e, TripPeople people, string viewerId, string tripOwnerId) => new(
        e.Id,
        e.Description,
        e.Category,
        people.Person(e.PaidBy),
        e.Amount,
        e.Currency,
        e.Rate,
        e.AmountInTripCurrency,
        e.Date,
        e.SplitMode,
        e.Shares.Select(s => new ExpenseShareResponse(s.UserId, people.NameOf(s.UserId), s.Amount)).ToList(),
        e.CreatedBy,
        e.CreatedAtUtc,
        e.CanEdit(viewerId, tripOwnerId));

    public static PaymentResponse ToResponse(this TripPayment p, TripPeople people, string viewerId, string tripOwnerId) => new(
        p.Id,
        people.Person(p.From),
        people.Person(p.To),
        p.Amount,
        p.Date,
        p.RecordedBy,
        p.CreatedAtUtc,
        p.CanDelete(viewerId, tripOwnerId));

    public static ExpenseSummaryResponse ToSummary(IReadOnlyList<TripExpense> expenses, IReadOnlyList<TripPayment> payments, string viewerId) => new(
        expenses.Sum(e => e.AmountInTripCurrency),
        LedgerBalances.Compute(expenses, payments, [viewerId]).First(b => b.UserId == viewerId).Net,
        expenses.Count);
}
