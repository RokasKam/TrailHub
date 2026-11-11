using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Gear;

/// <param name="Reason">Why it's suggested, shown under the item ("3 nights in huts").</param>
public sealed record GearSuggestion(string Text, ChecklistItemKind Kind, string Reason);
