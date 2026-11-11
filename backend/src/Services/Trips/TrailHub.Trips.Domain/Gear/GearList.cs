using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Gear;

/// <summary>
/// A reusable packing list ("My hut trip kit"). Adding it to a trip copies its items into the trip's checklist;
/// later edits to the list don't change trips it was already added to.
/// </summary>
public sealed class GearList : AggregateRoot<Guid>
{
    public const int MaxNameLength = 100;
    public const int MaxItems = 150;
    public const int MaxListsPerUser = 50;

    private List<GearItem> _items = [];

    private GearList()
    {
    }

    public string OwnerId { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public IReadOnlyList<GearItem> Items => _items;

    public DateTime UpdatedAtUtc { get; private set; }

    public static Result<GearList> Create(string ownerId, string name, IEnumerable<GearItem> items, DateTime nowUtc)
    {
        var list = new GearList { Id = Guid.CreateVersion7(), OwnerId = ownerId };
        var result = list.Update(name, items, nowUtc);
        return result.IsSuccess ? list : result.Error!;
    }

    /// <summary>Replaces the name and items. Blank and duplicate items (ignoring case) are dropped.</summary>
    public Result Update(string name, IEnumerable<GearItem> items, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            return GearErrors.InvalidName;
        }

        var cleaned = Normalize(items);
        if (cleaned.Any(i => i.Text.Length > Trip.MaxNameLength))
        {
            return GearErrors.ItemTooLong;
        }

        if (cleaned.Count > MaxItems)
        {
            return GearErrors.TooManyItems;
        }

        Name = name.Trim();
        _items = cleaned;
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    public static List<GearItem> Normalize(IEnumerable<GearItem> items) =>
        items
            .Where(i => !string.IsNullOrWhiteSpace(i.Text))
            .Select(i => i with { Text = i.Text.Trim() })
            .DistinctBy(i => i.Text.ToUpperInvariant())
            .ToList();
}
