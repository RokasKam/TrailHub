using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trips.Domain.Trips;

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
