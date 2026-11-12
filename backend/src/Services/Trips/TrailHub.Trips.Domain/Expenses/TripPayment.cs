using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Expenses;

/// <summary>
/// Money one participant gave another to settle up, in the trip currency. It moves both balances toward zero.
/// Whoever recorded it, either person in it, and the trip owner can delete it.
/// </summary>
public sealed class TripPayment : AggregateRoot<Guid>
{
    private Dictionary<string, string> _names = [];

    private TripPayment()
    {
    }

    public Guid TripId { get; private set; }

    public string From { get; private set; } = null!;

    public string To { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public DateOnly Date { get; private set; }

    public string RecordedBy { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Display names of both people when it was recorded, for people who later leave the trip.</summary>
    public IReadOnlyDictionary<string, string> KnownNames => _names;

    public static Result<TripPayment> Create(Guid tripId, string recordedBy, PaymentDetails details, LedgerContext context, DateTime nowUtc)
    {
        var from = details.From?.Trim();
        var to = details.To?.Trim();
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to
            || !Money.IsValidAmount(details.Amount, Money.MaxStoredAmount)
            || details.Date < TripExpense.EarliestDate || details.Date > DateOnly.FromDateTime(nowUtc).AddYears(1))
        {
            return TripErrors.InvalidPayment;
        }

        if (!context.Participants.ContainsKey(from) || !context.Participants.ContainsKey(to))
        {
            return TripErrors.NotAParticipant;
        }

        return new TripPayment
        {
            Id = Guid.CreateVersion7(),
            TripId = tripId,
            From = from,
            To = to,
            Amount = details.Amount,
            Date = details.Date,
            RecordedBy = recordedBy,
            CreatedAtUtc = nowUtc,
            _names = new Dictionary<string, string>(StringComparer.Ordinal) { [from] = context.Participants[from], [to] = context.Participants[to] },
        };
    }

    /// <summary>Whoever recorded it, the payer, the payee, and the trip owner.</summary>
    public bool CanDelete(string actorId, string tripOwnerId) => actorId == RecordedBy || actorId == From || actorId == To || actorId == tripOwnerId;
}
