using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Gear;

/// <summary>
/// Rule-based packing suggestions. Deliberately simple and explainable: every suggestion says why. Items already
/// on the checklist (same text, ignoring case) are left out.
/// </summary>
public static class GearSuggestions
{
    public static IReadOnlyList<GearSuggestion> For(TripGearFacts facts, IEnumerable<string> alreadyOnChecklist)
    {
        var have = alreadyOnChecklist.Select(t => t.Trim().ToUpperInvariant()).ToHashSet();
        var shared = facts.Shared ? ChecklistItemKind.Shared : ChecklistItemKind.Personal;
        var list = new List<GearSuggestion>();
        void Add(string text, ChecklistItemKind kind, string reason)
        {
            if (!have.Contains(text.ToUpperInvariant()) && list.All(s => !s.Text.Equals(text, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new GearSuggestion(text, kind, reason));
            }
        }

        var nights = Math.Max(0, facts.Days - 1);
        string Nights(int n, string where) => $"{n} night{(n == 1 ? "" : "s")} {where}";

        // Always useful in the mountains.
        Add("First-aid kit", shared, "Every hike");
        Add("Rain jacket", ChecklistItemKind.Personal, "Every hike");
        Add("Headlamp", ChecklistItemKind.Personal, "Every hike");

        if (facts.TentNights > 0)
        {
            var why = Nights(facts.TentNights, "camping");
            Add("Tent", shared, why);
            Add("Sleeping bag", ChecklistItemKind.Personal, why);
            Add("Sleeping mat", ChecklistItemKind.Personal, why);
            Add("Stove and fuel", shared, why);
            Add("Water filter", shared, why);
            Add("Toilet trowel and paper", ChecklistItemKind.Personal, why);
        }

        if (facts.HutNights > 0)
        {
            var why = Nights(facts.HutNights, "in huts");
            Add("Sleeping bag liner", ChecklistItemKind.Personal, why);
            Add("Earplugs", ChecklistItemKind.Personal, why);
            Add("Cash for the huts", ChecklistItemKind.Personal, why);
            Add("Hut reservations", ChecklistItemKind.Personal, why);
        }

        if (facts.HotelNights > 0)
        {
            Add("Booking confirmations", ChecklistItemKind.Personal, Nights(facts.HotelNights, "in hotels"));
        }

        if (nights > 0)
        {
            var why = Nights(nights, "away");
            Add("Power bank", ChecklistItemKind.Personal, why);
            Add("Toiletries (small)", ChecklistItemKind.Personal, why);
            Add($"Spare socks ({Math.Min(nights + 1, 4)} pairs)", ChecklistItemKind.Personal, why);
        }

        if (facts.MaxElevationMeters is { } top)
        {
            var high = $"Up to {Math.Round(top / 100) * 100:0} m";
            if (top >= 2000)
            {
                Add("Warm layer (fleece or down)", ChecklistItemKind.Personal, high);
                Add("Gloves and warm hat", ChecklistItemKind.Personal, high);
                Add("Sun cream and sunglasses", ChecklistItemKind.Personal, high);
            }

            if (top >= 2800)
            {
                Add("Microspikes", ChecklistItemKind.Personal, $"{high}: snow can linger all summer");
            }
        }

        if (facts.LongestDayMinutes is >= 360)
        {
            var hours = facts.LongestDayMinutes.Value / 60.0;
            Add("Water (2+ L)", ChecklistItemKind.Personal, $"Longest day ~{hours:0.#} h of walking");
            Add("Extra snacks", ChecklistItemKind.Personal, $"Longest day ~{hours:0.#} h of walking");
        }

        return list;
    }
}
