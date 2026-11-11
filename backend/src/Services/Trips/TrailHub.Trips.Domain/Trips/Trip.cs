using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trips.Domain.Trips;

/// <summary>
/// A planned (possibly multi-day) hiking trip with a per-day itinerary and a packing checklist.
/// Linked to a group, its members can see it and use the checklist; the itinerary stays the owner's.
/// Membership itself is checked by the application layer, which knows the group's members.
/// </summary>
public sealed class Trip : AggregateRoot<Guid>
{
    public const int MaxNameLength = 200;
    public const int MaxTextLength = 4000;
    public const int MaxDays = 30;
    public const int MaxChecklistItems = 200;

    private readonly List<TripDay> _days = [];
    private readonly List<ChecklistItem> _checklist = [];

    private Trip()
    {
    }

    public string OwnerId { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public TripStatus Status { get; private set; }

    /// <summary>The group (in the Groups service) this trip is shared with, if any.</summary>
    public Guid? GroupId { get; private set; }

    /// <summary>A planned route (in the Routes service) for the whole trip; its day N is the trip's day N.</summary>
    public Guid? RouteId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<TripDay> Days => _days;

    public IReadOnlyList<ChecklistItem> Checklist => _checklist;

    public int DayCount => EndDate.DayNumber - StartDate.DayNumber + 1;

    public static Result<Trip> Create(string ownerId, string name, string? description, DateOnly startDate, DateOnly endDate, DateTime nowUtc)
    {
        var error = ValidateDetails(name, description) ?? ValidateDates(startDate, endDate);
        if (error is not null)
        {
            return error;
        }

        var trip = new Trip
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            Status = TripStatus.Planned,
            CreatedAtUtc = nowUtc,
        };
        trip.SyncDays();
        return trip;
    }

    public Result UpdateDetails(string name, string? description)
    {
        if (EnsurePlanned() is { } notPlanned)
        {
            return notPlanned;
        }

        if (ValidateDetails(name, description) is { } error)
        {
            return error;
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return Result.Success();
    }

    /// <summary>Changes the dates. Existing day plans keep their day number; days beyond the new length are dropped.</summary>
    public Result Reschedule(DateOnly startDate, DateOnly endDate)
    {
        var error = EnsurePlanned() ?? ValidateDates(startDate, endDate);
        if (error is not null)
        {
            return error;
        }

        StartDate = startDate;
        EndDate = endDate;
        SyncDays();
        return Result.Success();
    }

    public Result AddTrail(int dayNumber, Guid trailId)
    {
        if (EnsurePlanned() is { } notPlanned)
        {
            return notPlanned;
        }

        if (FindDay(dayNumber) is not { } day)
        {
            return TripErrors.DayNotFound;
        }

        if (day.TrailIds.Contains(trailId))
        {
            return TripErrors.TrailAlreadyOnDay;
        }

        if (day.TrailIds.Count >= TripDay.MaxTrailsPerDay)
        {
            return TripErrors.TooManyTrails;
        }

        day.TrailIds.Add(trailId);
        return Result.Success();
    }

    public Result RemoveTrail(int dayNumber, Guid trailId)
    {
        if (EnsurePlanned() is { } notPlanned)
        {
            return notPlanned;
        }

        return FindDay(dayNumber) is { } day && day.TrailIds.Remove(trailId) ? Result.Success() : TripErrors.TrailNotOnDay;
    }

    public Result UpdateDay(int dayNumber, string? notes, string? overnight)
    {
        if (EnsurePlanned() is { } notPlanned)
        {
            return notPlanned;
        }

        if (notes?.Length > MaxTextLength || overnight?.Length > MaxNameLength)
        {
            return TripErrors.TextTooLong;
        }

        if (FindDay(dayNumber) is not { } day)
        {
            return TripErrors.DayNotFound;
        }

        day.Update(notes, overnight);
        return Result.Success();
    }

    public Result AttachRoute(Guid? routeId)
    {
        if (EnsurePlanned() is { } notPlanned)
        {
            return notPlanned;
        }

        RouteId = routeId;
        return Result.Success();
    }

    public Result LinkGroup(Guid groupId)
    {
        if (EnsurePlanned() is { } notPlanned)
        {
            return notPlanned;
        }

        if (GroupId is { } current && current != groupId)
        {
            ReleaseEveryoneBut([OwnerId]);
        }

        GroupId = groupId;
        return Result.Success();
    }

    /// <summary>Makes the trip private again. Other people's claims and ticks are dropped; shared items stay.</summary>
    public Result UnlinkGroup()
    {
        if (GroupId is null)
        {
            return Result.Success();
        }

        ReleaseEveryoneBut([OwnerId]);
        GroupId = null;
        return Result.Success();
    }

    /// <summary>
    /// Brings the trip in line with the group's current members: people who left lose their claims and ticks.
    /// If the owner left the group, the trip goes back to being private. Returns whether anything changed.
    /// </summary>
    public bool SyncGroupMembers(IReadOnlyCollection<string> memberIds)
    {
        if (GroupId is null)
        {
            return false;
        }

        if (!memberIds.Contains(OwnerId))
        {
            UnlinkGroup();
            return true;
        }

        return ReleaseEveryoneBut(memberIds);
    }

    public Result<ChecklistItem> AddChecklistItem(string actorId, string text, ChecklistItemKind kind = ChecklistItemKind.Personal)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxNameLength)
        {
            return TripErrors.InvalidChecklistItem;
        }

        if (kind == ChecklistItemKind.Shared && GroupId is null)
        {
            return TripErrors.SharedItemNeedsGroup;
        }

        if (_checklist.Count >= MaxChecklistItems)
        {
            return TripErrors.ChecklistFull;
        }

        var item = new ChecklistItem(text.Trim(), kind, actorId);
        _checklist.Add(item);
        return item;
    }

    /// <summary>
    /// Adds several items at once (a gear list or suggestions). Items already on the checklist (same text, ignoring
    /// case) are skipped; on a private trip, group gear becomes personal. Stops quietly when the checklist is full.
    /// Returns how many were added.
    /// </summary>
    public int AddChecklistItems(string actorId, IEnumerable<(string Text, ChecklistItemKind Kind)> items)
    {
        var have = _checklist.Select(i => i.Text.ToUpperInvariant()).ToHashSet();
        var added = 0;
        foreach (var (text, kind) in items)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxNameLength || !have.Add(text.Trim().ToUpperInvariant()))
            {
                continue;
            }

            if (_checklist.Count >= MaxChecklistItems)
            {
                break;
            }

            _checklist.Add(new ChecklistItem(text.Trim(), GroupId is null ? ChecklistItemKind.Personal : kind, actorId));
            added++;
        }

        return added;
    }

    /// <summary>
    /// Personal items: ticks the actor's own copy. Shared items: only the person bringing it can tick it;
    /// ticking an unclaimed shared item claims it at the same time.
    /// </summary>
    public Result SetChecklistItemDone(string actorId, Guid itemId, bool isDone)
    {
        if (_checklist.Find(i => i.Id == itemId) is not { } item)
        {
            return TripErrors.ChecklistItemNotFound;
        }

        if (item.Kind == ChecklistItemKind.Personal)
        {
            item.DoneBy.Remove(actorId);
            if (isDone)
            {
                item.DoneBy.Add(actorId);
            }

            return Result.Success();
        }

        if (item.ClaimedBy is { } claimer && claimer != actorId)
        {
            return TripErrors.ClaimedBySomeoneElse;
        }

        if (isDone)
        {
            item.ClaimedBy = actorId;
        }

        item.IsDone = isDone;
        return Result.Success();
    }

    /// <summary>"I'll bring it" on a shared item, or taking that back. The owner can also release someone else's claim.</summary>
    public Result ClaimChecklistItem(string actorId, Guid itemId, bool claim)
    {
        if (_checklist.Find(i => i.Id == itemId) is not { } item)
        {
            return TripErrors.ChecklistItemNotFound;
        }

        if (item.Kind != ChecklistItemKind.Shared)
        {
            return TripErrors.NotASharedItem;
        }

        if (claim)
        {
            if (item.ClaimedBy is { } claimer && claimer != actorId)
            {
                return TripErrors.ClaimedBySomeoneElse;
            }

            item.ClaimedBy = actorId;
            return Result.Success();
        }

        if (item.ClaimedBy is { } current && current != actorId && actorId != OwnerId)
        {
            return TripErrors.ClaimedBySomeoneElse;
        }

        item.ClaimedBy = null;
        item.IsDone = false;
        return Result.Success();
    }

    /// <summary>The trip owner can remove any item; everyone else only the ones they added.</summary>
    public Result RemoveChecklistItem(string actorId, Guid itemId)
    {
        if (_checklist.Find(i => i.Id == itemId) is not { } item)
        {
            return TripErrors.ChecklistItemNotFound;
        }

        if (actorId != OwnerId && actorId != item.AddedBy)
        {
            return TripErrors.NotYourItem;
        }

        _checklist.Remove(item);
        return Result.Success();
    }

    public bool CanRemoveChecklistItem(string actorId, ChecklistItem item) => actorId == OwnerId || actorId == item.AddedBy;

    private bool ReleaseEveryoneBut(IReadOnlyCollection<string> keep)
    {
        var changed = false;
        foreach (var item in _checklist)
        {
            if (item.ClaimedBy is { } claimer && !keep.Contains(claimer))
            {
                item.ClaimedBy = null;
                item.IsDone = false;
                changed = true;
            }

            changed |= item.DoneBy.RemoveAll(u => !keep.Contains(u)) > 0;
        }

        return changed;
    }

    public Result Complete() => Transition(TripStatus.Completed);

    public Result Cancel() => Transition(TripStatus.Cancelled);

    private Result Transition(TripStatus to)
    {
        if (EnsurePlanned() is { } notPlanned)
        {
            return notPlanned;
        }

        Status = to;
        return Result.Success();
    }

    private TripDay? FindDay(int dayNumber) => _days.Find(d => d.DayNumber == dayNumber);

    private void SyncDays()
    {
        _days.RemoveAll(d => d.DayNumber > DayCount);
        for (var n = _days.Count + 1; n <= DayCount; n++)
        {
            _days.Add(new TripDay(n));
        }
    }

    private Error? EnsurePlanned() => Status == TripStatus.Planned ? null : TripErrors.NotEditable;

    private static Error? ValidateDetails(string name, string? description) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength ? TripErrors.InvalidName
        : description?.Length > MaxTextLength ? TripErrors.TextTooLong
        : null;

    private static Error? ValidateDates(DateOnly start, DateOnly end) =>
        end < start ? TripErrors.EndBeforeStart
        : end.DayNumber - start.DayNumber + 1 > MaxDays ? TripErrors.TooLong
        : null;
}
