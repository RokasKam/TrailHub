using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Domain.Community;

public enum ReportReason
{
    /// <summary>The line doesn't match the paths, or it leads somewhere it shouldn't.</summary>
    Inaccurate,

    /// <summary>Sends people through dangerous or closed terrain.</summary>
    Dangerous,

    /// <summary>Offensive name, description or photos.</summary>
    Inappropriate,

    Spam,
    Other,
}
