using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trips.Domain.Trips;

public enum TripStatus
{
    Planned,
    Completed,
    Cancelled,
}

public sealed class TripDay
{
    public const int MaxTrailsPerDay = 10;

    private TripDay()
    {
    }

    internal TripDay(int dayNumber) => DayNumber = dayNumber;

    public int DayNumber { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Where the group sleeps after this day (hut, campsite, town).</summary>
    public string? Overnight { get; private set; }

    /// <summary>Trails from the Trails service, in hiking order. Stored by id only; services don't share databases.</summary>
    public List<Guid> TrailIds { get; private set; } = [];

    internal void Update(string? notes, string? overnight)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Overnight = string.IsNullOrWhiteSpace(overnight) ? null : overnight.Trim();
    }
}

public sealed class ChecklistItem : Entity<Guid>
{
    private ChecklistItem()
    {
    }

    internal ChecklistItem(string text)
    {
        Id = Guid.CreateVersion7();
        Text = text;
    }

    public string Text { get; private set; } = null!;

    public bool IsDone { get; internal set; }
}

/// <summary>A planned (possibly multi-day) hiking trip with a per-day itinerary and a packing checklist.</summary>
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

    public Result<ChecklistItem> AddChecklistItem(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxNameLength)
        {
            return TripErrors.InvalidChecklistItem;
        }

        if (_checklist.Count >= MaxChecklistItems)
        {
            return TripErrors.ChecklistFull;
        }

        var item = new ChecklistItem(text.Trim());
        _checklist.Add(item);
        return item;
    }

    public Result SetChecklistItemDone(Guid itemId, bool isDone)
    {
        if (_checklist.Find(i => i.Id == itemId) is not { } item)
        {
            return TripErrors.ChecklistItemNotFound;
        }

        item.IsDone = isDone;
        return Result.Success();
    }

    public Result RemoveChecklistItem(Guid itemId) =>
        _checklist.RemoveAll(i => i.Id == itemId) > 0 ? Result.Success() : TripErrors.ChecklistItemNotFound;

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

public static class TripErrors
{
    public static readonly Error NotFound = Error.NotFound("Trip.NotFound", "The trip does not exist.");
    public static readonly Error InvalidName = Error.Validation("Trip.InvalidName", $"Name is required and must be at most {Trip.MaxNameLength} characters.");
    public static readonly Error TextTooLong = Error.Validation("Trip.TextTooLong", "Text is too long.");
    public static readonly Error EndBeforeStart = Error.Validation("Trip.EndBeforeStart", "The trip cannot end before it starts.");
    public static readonly Error TooLong = Error.Validation("Trip.TooLong", $"A trip can be at most {Trip.MaxDays} days.");
    public static readonly Error NotEditable = Error.Conflict("Trip.NotEditable", "Completed or cancelled trips can't be changed.");
    public static readonly Error DayNotFound = Error.NotFound("Trip.DayNotFound", "The trip has no such day.");
    public static readonly Error TrailAlreadyOnDay = Error.Conflict("Trip.TrailAlreadyOnDay", "That trail is already planned for this day.");
    public static readonly Error TrailNotOnDay = Error.NotFound("Trip.TrailNotOnDay", "That trail isn't planned for this day.");
    public static readonly Error TooManyTrails = Error.Validation("Trip.TooManyTrails", $"A day can have at most {TripDay.MaxTrailsPerDay} trails.");
    public static readonly Error InvalidChecklistItem = Error.Validation("Trip.InvalidChecklistItem", "Checklist item text is required.");
    public static readonly Error ChecklistFull = Error.Validation("Trip.ChecklistFull", $"A checklist can have at most {Trip.MaxChecklistItems} items.");
    public static readonly Error ChecklistItemNotFound = Error.NotFound("Trip.ChecklistItemNotFound", "The checklist item does not exist.");
}
