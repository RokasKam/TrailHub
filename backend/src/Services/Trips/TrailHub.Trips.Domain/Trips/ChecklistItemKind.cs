using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trips.Domain.Trips;

public enum ChecklistItemKind
{
    /// <summary>Everyone brings their own (headlamp, water). Each person ticks it off for themselves.</summary>
    Personal,

    /// <summary>One person brings it for the group (stove, tent). Someone claims it, then ticks it when packed.</summary>
    Shared,
}
