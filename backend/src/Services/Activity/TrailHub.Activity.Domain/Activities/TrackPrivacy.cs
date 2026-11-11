namespace TrailHub.Activity.Domain.Activities;

/// <summary>How much of a shared hike's track group members see. The author always sees the full track.</summary>
public enum TrackPrivacy
{
    /// <summary>The whole track.</summary>
    Full,

    /// <summary>Drops the points near the start and the finish (usually home).</summary>
    HideEnds,

    /// <summary>No map at all, only the stats.</summary>
    StatsOnly,
}
