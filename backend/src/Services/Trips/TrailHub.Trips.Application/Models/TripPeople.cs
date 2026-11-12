using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Models;

/// <summary>
/// Who takes part in a trip's expenses and what to call everyone in its ledger. Participants are the owner plus, for a
/// group trip, the group's current members. People who left keep the name they last had in the ledger.
/// </summary>
public sealed class TripPeople
{
    public const string FormerMember = "Former member";

    private readonly Dictionary<string, string> _current = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _known = new(StringComparer.Ordinal);

    /// <param name="viewerName">The caller's own name, used for the owner of a private trip.</param>
    public TripPeople(Trip trip, GroupInfo? group, string viewerId, string? viewerName, IEnumerable<TripExpense> expenses, IEnumerable<TripPayment> payments)
    {
        var history = expenses.Select(e => (At: e.UpdatedAtUtc, Names: e.KnownNames))
            .Concat(payments.Select(p => (At: p.CreatedAtUtc, Names: p.KnownNames)))
            .OrderBy(x => x.At);
        foreach (var (_, names) in history)
        {
            foreach (var (userId, name) in names)
            {
                _known[userId] = name;
            }
        }

        foreach (var member in group?.Members ?? [])
        {
            _current[member.UserId] = member.DisplayName;
        }

        if (!_current.ContainsKey(trip.OwnerId))
        {
            _current[trip.OwnerId] = _known.GetValueOrDefault(trip.OwnerId)
                ?? (trip.OwnerId == viewerId && !string.IsNullOrWhiteSpace(viewerName) ? viewerName : "Trip owner");
        }

        Participants = _current
            .Select(p => new GroupMemberInfo(p.Key, p.Value))
            .OrderByDescending(p => p.UserId == trip.OwnerId)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The owner first, then everyone else by name.</summary>
    public IReadOnlyList<GroupMemberInfo> Participants { get; }

    public IReadOnlyDictionary<string, string> Current => _current;

    public bool IsParticipant(string userId) => _current.ContainsKey(userId);

    public string NameOf(string userId) => _current.GetValueOrDefault(userId) ?? _known.GetValueOrDefault(userId) ?? FormerMember;
}
