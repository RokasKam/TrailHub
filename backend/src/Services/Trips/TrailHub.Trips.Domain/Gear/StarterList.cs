using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Gear;

/// <summary>A built-in, read-only gear list (see <see cref="StarterLists"/>).</summary>
public sealed record StarterList(string Id, string Name, IReadOnlyList<GearItem> Items);
