using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Trails.Domain.Trails;

/// <summary>Who took a trail out of Discover; only the same kind of decision brings it back automatically.</summary>
public enum HiddenBy
{
    /// <summary>The author unpublished it (from the Routes service).</summary>
    Author,

    /// <summary>Hidden automatically after <see cref="Trail.AutoHideReportCount"/> people reported it.</summary>
    Reports,

    /// <summary>A moderator hid it.</summary>
    Moderator,
}
