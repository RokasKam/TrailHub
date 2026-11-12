using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Mappings;
using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Services;

internal sealed class TripExpenseService(
    TripEditor editor,
    ITripExpenseRepository expenses,
    ITripPaymentRepository payments,
    IExchangeRateService exchangeRates,
    IUnitOfWork unitOfWork,
    ITripChangeNotifier notifier,
    ICurrentUser user,
    TimeProvider clock) : ITripExpenseService
{
    public async Task<TripExpensesResponse> GetAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var (trip, group) = await editor.LoadVisibleAsync(tripId, cancellationToken);
        var tripExpenses = await expenses.GetByTripAsync(trip.Id, cancellationToken);
        var tripPayments = await payments.GetByTripAsync(trip.Id, cancellationToken);
        var people = editor.PeopleOf(trip, group, tripExpenses, tripPayments);
        var balances = LedgerBalances.Compute(tripExpenses, tripPayments, people.Participants.Select(p => p.UserId));

        return new TripExpensesResponse(
            trip.Currency,
            tripExpenses.Sum(e => e.AmountInTripCurrency),
            tripExpenses
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.CreatedAtUtc)
                .Select(e => e.ToResponse(people, user.UserId, trip.OwnerId))
                .ToList(),
            tripPayments
                .OrderByDescending(p => p.Date)
                .ThenByDescending(p => p.CreatedAtUtc)
                .Select(p => p.ToResponse(people, user.UserId, trip.OwnerId))
                .ToList(),
            balances
                .Select(b => new BalanceResponse(b.UserId, people.NameOf(b.UserId), b.Paid, b.Share, b.Net, b.UserId == user.UserId))
                .OrderByDescending(b => b.Net)
                .ThenBy(b => b.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            SettlementPlanner.Suggest(balances)
                .Select(s => new SettlementSuggestionResponse(people.Person(s.From), people.Person(s.To), s.Amount))
                .ToList(),
            people.Participants.Select(p => new PersonResponse(p.UserId, p.DisplayName)).ToList());
    }

    public async Task<ExpenseResponse> AddExpenseAsync(Guid tripId, ExpenseRequest request, CancellationToken cancellationToken)
    {
        var (trip, group) = await editor.LoadVisibleAsync(tripId, cancellationToken);
        await EnsureSupportedCurrencyAsync(request.Currency, trip.Currency, cancellationToken);
        var people = editor.PeopleOf(trip, group, [], []);

        var expense = TripExpense.Create(trip.Id, user.UserId, ToDetails(request), Context(trip, people.Current), Now()).GetValueOrThrow();
        expenses.Add(expense);
        await SaveAsync(trip.Id, cancellationToken);
        return expense.ToResponse(people, user.UserId, trip.OwnerId);
    }

    public async Task<ExpenseResponse> UpdateExpenseAsync(Guid tripId, Guid expenseId, ExpenseRequest request, CancellationToken cancellationToken)
    {
        var (trip, group) = await editor.LoadVisibleAsync(tripId, cancellationToken);
        var expense = await GetExpenseAsync(trip.Id, expenseId, cancellationToken);
        if (!expense.CanEdit(user.UserId, trip.OwnerId))
        {
            throw TripErrors.NotExpenseEditor.ToException();
        }

        await EnsureSupportedCurrencyAsync(request.Currency, trip.Currency, cancellationToken);
        var people = editor.PeopleOf(trip, group, [], []);

        expense.Update(user.UserId, trip.OwnerId, ToDetails(request), Context(trip, people.Current), Now()).ThrowIfFailure();
        await SaveAsync(trip.Id, cancellationToken);
        return expense.ToResponse(people, user.UserId, trip.OwnerId);
    }

    public async Task DeleteExpenseAsync(Guid tripId, Guid expenseId, CancellationToken cancellationToken)
    {
        var (trip, _) = await editor.LoadVisibleAsync(tripId, cancellationToken);
        var expense = await GetExpenseAsync(trip.Id, expenseId, cancellationToken);
        if (!expense.CanEdit(user.UserId, trip.OwnerId))
        {
            throw TripErrors.NotExpenseEditor.ToException();
        }

        expenses.Remove(expense);
        await SaveAsync(trip.Id, cancellationToken);
    }

    public async Task<PaymentResponse> RecordPaymentAsync(Guid tripId, PaymentRequest request, CancellationToken cancellationToken)
    {
        var (trip, group) = await editor.LoadVisibleAsync(tripId, cancellationToken);
        var people = editor.PeopleOf(trip, group, [], []);

        var payment = TripPayment.Create(
                trip.Id,
                user.UserId,
                new PaymentDetails(request.From, request.To, request.Amount, request.Date),
                Context(trip, people.Current),
                Now())
            .GetValueOrThrow();
        payments.Add(payment);
        await SaveAsync(trip.Id, cancellationToken);
        return payment.ToResponse(people, user.UserId, trip.OwnerId);
    }

    public async Task DeletePaymentAsync(Guid tripId, Guid paymentId, CancellationToken cancellationToken)
    {
        var (trip, _) = await editor.LoadVisibleAsync(tripId, cancellationToken);
        var payment = await payments.GetByIdAsync(paymentId, cancellationToken) is { } found && found.TripId == trip.Id
            ? found
            : throw TripErrors.PaymentNotFound.ToException();
        if (!payment.CanDelete(user.UserId, trip.OwnerId))
        {
            throw TripErrors.NotPaymentEditor.ToException();
        }

        payments.Remove(payment);
        await SaveAsync(trip.Id, cancellationToken);
    }

    private async Task<TripExpense> GetExpenseAsync(Guid tripId, Guid expenseId, CancellationToken cancellationToken) =>
        await expenses.GetByIdAsync(expenseId, cancellationToken) is { } expense && expense.TripId == tripId
            ? expense
            : throw TripErrors.ExpenseNotFound.ToException();

    /// <summary>The trip's own currency is always fine; any other must be one the rate provider knows.</summary>
    private async Task EnsureSupportedCurrencyAsync(string? currency, string tripCurrency, CancellationToken cancellationToken)
    {
        var code = CurrencyCode.Normalize(currency) ?? throw TripErrors.InvalidCurrency.ToException();
        if (code != tripCurrency && !(await exchangeRates.GetCurrenciesAsync(cancellationToken)).Any(c => c.Code == code))
        {
            throw TripErrors.InvalidCurrency.ToException();
        }
    }

    private async Task SaveAsync(Guid tripId, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TripChangedAsync(tripId, cancellationToken);
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private static LedgerContext Context(Trip trip, IReadOnlyDictionary<string, string> participants) => new(trip.Currency, participants);

    private static ExpenseDetails ToDetails(ExpenseRequest r) => new(
        r.Description,
        r.Category ?? ExpenseCategory.Other,
        r.PaidBy,
        r.Amount,
        r.Currency,
        r.Rate,
        r.Date,
        r.SplitMode ?? SplitMode.Equal,
        r.Participants,
        r.CustomShares?.Select(s => new CustomShare(s.UserId, s.Amount)).ToList());
}
