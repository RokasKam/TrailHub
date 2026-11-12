using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Expenses;

/// <summary>
/// A cost someone paid on a trip, split among participants. It may be in another currency than the trip's; then the
/// rate converts it, and the shares are in the trip currency. Anyone who can see the trip can log one; the person who
/// logged it, the person who paid, and the trip owner can change or delete it.
/// </summary>
public sealed class TripExpense : AggregateRoot<Guid>
{
    public const int MaxDescriptionLength = 120;

    /// <summary>Expenses dated before this are treated as typos.</summary>
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    private List<ExpenseShare> _shares = [];
    private Dictionary<string, string> _names = [];

    private TripExpense()
    {
    }

    public Guid TripId { get; private set; }

    public string Description { get; private set; } = null!;

    public ExpenseCategory Category { get; private set; }

    public string PaidBy { get; private set; } = null!;

    /// <summary>In <see cref="Currency"/>.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = null!;

    /// <summary>Trip-currency units per one unit of <see cref="Currency"/>; 1 when they're the same.</summary>
    public decimal Rate { get; private set; }

    public decimal AmountInTripCurrency { get; private set; }

    public DateOnly Date { get; private set; }

    public SplitMode SplitMode { get; private set; }

    /// <summary>In the trip currency; they add up to <see cref="AmountInTripCurrency"/>.</summary>
    public IReadOnlyList<ExpenseShare> Shares => _shares;

    /// <summary>Display names of the people involved when it was last saved, for people who later leave the trip.</summary>
    public IReadOnlyDictionary<string, string> KnownNames => _names;

    public string CreatedBy { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static Result<TripExpense> Create(Guid tripId, string createdBy, ExpenseDetails details, LedgerContext context, DateTime nowUtc)
    {
        var expense = new TripExpense { Id = Guid.CreateVersion7(), TripId = tripId, CreatedBy = createdBy, CreatedAtUtc = nowUtc };
        var result = expense.Apply(details, context, nowUtc);
        return result.IsSuccess ? expense : result.Error!;
    }

    /// <summary>Replaces everything about the expense. Only <see cref="CanEdit"/> people may.</summary>
    public Result Update(string actorId, string tripOwnerId, ExpenseDetails details, LedgerContext context, DateTime nowUtc) =>
        CanEdit(actorId, tripOwnerId) ? Apply(details, context, nowUtc) : TripErrors.NotExpenseEditor;

    /// <summary>Whoever logged it, whoever paid it, and the trip owner.</summary>
    public bool CanEdit(string actorId, string tripOwnerId) => actorId == CreatedBy || actorId == PaidBy || actorId == tripOwnerId;

    private Result Apply(ExpenseDetails details, LedgerContext context, DateTime nowUtc)
    {
        var description = details.Description?.Trim();
        var today = DateOnly.FromDateTime(nowUtc);
        if (string.IsNullOrEmpty(description) || description.Length > MaxDescriptionLength
            || !Money.IsValidAmount(details.Amount, Money.MaxExpenseAmount)
            || details.Date < EarliestDate || details.Date > today.AddYears(1)
            || !Enum.IsDefined(details.Category) || !Enum.IsDefined(details.SplitMode))
        {
            return TripErrors.InvalidExpense;
        }

        if (CurrencyCode.Normalize(details.Currency) is not { } currency)
        {
            return TripErrors.InvalidCurrency;
        }

        decimal rate = 1;
        if (currency != context.TripCurrency)
        {
            if (details.Rate is not { } given)
            {
                return TripErrors.RateRequired;
            }

            if (given <= 0 || given > Money.MaxRate || !Money.HasAtMostDecimals(given, Money.RateDecimals))
            {
                return TripErrors.InvalidRate;
            }

            rate = given;
        }

        var converted = Money.Convert(details.Amount, rate);
        if (converted < 0.01m || converted > Money.MaxStoredAmount)
        {
            return TripErrors.InvalidRate;
        }

        var paidBy = details.PaidBy?.Trim();
        if (string.IsNullOrEmpty(paidBy) || !context.Participants.ContainsKey(paidBy))
        {
            return TripErrors.NotAParticipant;
        }

        var shares = details.SplitMode == SplitMode.Equal
            ? SplitEqually(details.Participants ?? [], converted, context)
            : SplitCustom(details.CustomShares ?? [], details.Amount, rate, converted, context);
        if (shares.IsFailure)
        {
            return shares.Error!;
        }

        Description = description;
        Category = details.Category;
        PaidBy = paidBy;
        Amount = details.Amount;
        Currency = currency;
        Rate = rate;
        AmountInTripCurrency = converted;
        Date = details.Date;
        SplitMode = details.SplitMode;
        _shares = shares.Value.ToList();
        _names = _shares.Select(s => s.UserId).Append(paidBy).Distinct(StringComparer.Ordinal)
            .ToDictionary(u => u, u => context.Participants[u], StringComparer.Ordinal);
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    private static Result<IReadOnlyList<ExpenseShare>> SplitEqually(IReadOnlyList<string> participants, decimal total, LedgerContext context)
    {
        var people = participants.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
        return people.Any(p => !context.Participants.ContainsKey(p))
            ? TripErrors.NotAParticipant
            : ExpenseSplitter.Equal(total, people);
    }

    private static Result<IReadOnlyList<ExpenseShare>> SplitCustom(IReadOnlyList<CustomShare> shares, decimal amount, decimal rate, decimal total, LedgerContext context)
    {
        var trimmed = shares.Select(s => s with { UserId = s.UserId?.Trim() }).ToList();
        return trimmed.Any(s => !string.IsNullOrEmpty(s.UserId) && !context.Participants.ContainsKey(s.UserId))
            ? TripErrors.NotAParticipant
            : ExpenseSplitter.Custom(trimmed, amount, rate, total);
    }
}
