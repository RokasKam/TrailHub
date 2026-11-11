using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Gear;

/// <param name="Kind">Personal: everyone brings their own. Shared: one person brings it for the group.</param>
public sealed record GearItem(string Text, ChecklistItemKind Kind);
