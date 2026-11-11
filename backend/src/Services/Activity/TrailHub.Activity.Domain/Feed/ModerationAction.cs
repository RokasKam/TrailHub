namespace TrailHub.Activity.Domain.Feed;

/// <summary>A moderator's decision on reported content. Each one resolves the content's open reports.</summary>
public enum ModerationAction
{
    /// <summary>Show it again.</summary>
    Restore,

    /// <summary>Hide it from everyone but its author and moderators.</summary>
    Hide,

    /// <summary>Remove it (and a photo's stored file).</summary>
    Delete,
}
