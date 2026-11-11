namespace TrailHub.Activity.Domain.Feed;

/// <summary>Why a comment or hike photo is hidden. Hidden content is only shown to its author and moderators.</summary>
public enum HiddenBy
{
    /// <summary>Hidden automatically after <see cref="ContentReport.AutoHideReportCount"/> people reported it.</summary>
    Reports,

    /// <summary>A moderator hid it.</summary>
    Moderator,
}
