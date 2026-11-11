using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Gear;

/// <summary>Built-in lists everyone starts with. Read-only; adding one to a trip works like any other list.</summary>
public static class StarterLists
{
    private static GearItem P(string text) => new(text, ChecklistItemKind.Personal);

    private static GearItem S(string text) => new(text, ChecklistItemKind.Shared);

    public static readonly IReadOnlyList<StarterList> All =
    [
        new("day-hike", "Day hike essentials",
        [
            P("Backpack"), P("Water (1.5–2 L)"), P("Snacks and lunch"), P("Rain jacket"), P("Warm layer"), P("Sun cream"),
            P("Sunglasses"), P("Hat"), P("Phone, charged"), P("Headlamp"), P("Map / offline map"), S("First-aid kit"),
        ]),
        new("hut-trip", "Hut-to-hut trip",
        [
            P("Sleeping bag liner"), P("Earplugs"), P("Hut slippers / light shoes"), P("Cash for the huts"), P("ID / Alpine club card"),
            P("Toiletries (small)"), P("Quick-dry towel"), P("Spare socks"), P("Change of clothes for the evening"), P("Power bank"),
            P("Hut reservations"), S("First-aid kit"),
        ]),
        new("camping", "Camping trip",
        [
            S("Tent"), S("Stove and fuel"), S("Lighter"), S("Cooking pot"), S("Water filter"), P("Sleeping bag"), P("Sleeping mat"),
            P("Spoon and mug"), P("Dinners and breakfasts"), P("Toilet trowel and paper"), P("Headlamp"), P("Warm hat"), S("First-aid kit"),
        ]),
    ];

    public static StarterList? Find(string id) => All.FirstOrDefault(s => s.Id == id);
}
