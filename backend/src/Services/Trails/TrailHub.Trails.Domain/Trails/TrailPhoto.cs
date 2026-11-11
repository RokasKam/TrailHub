using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trails.Domain.Trails;

/// <summary>A photo on a trail, added by any signed-in hiker. Reported photos can be hidden or removed.</summary>
public sealed class TrailPhoto : Entity<Guid>
{
    private TrailPhoto()
    {
    }

    /// <summary>Either an external https URL or a key in TrailHub's photo storage (resolved to a URL when read).</summary>
    public string Url { get; private set; } = null!;

    public string? Caption { get; private set; }

    public string UploadedBy { get; private set; } = null!;

    public DateTime UploadedAtUtc { get; private set; }

    /// <summary>Set while the photo is hidden; hidden photos are only shown to their uploader and moderators.</summary>
    public HiddenBy? HiddenBy { get; private set; }

    public bool IsHidden => HiddenBy is not null;

    /// <summary>Whether the photo is stored in TrailHub's photo storage (rather than linked from elsewhere).</summary>
    public bool IsStored => !Uri.TryCreate(Url, UriKind.Absolute, out _);

    /// <summary>Hidden photos exist only for their uploader and moderators.</summary>
    public bool IsVisibleTo(string userId, bool isModerator) => !IsHidden || UploadedBy == userId || isModerator;

    internal static TrailPhoto Create(string url, string? caption, string uploadedBy, DateTime nowUtc) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Url = url,
            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
            UploadedBy = uploadedBy,
            UploadedAtUtc = nowUtc,
        };

    internal void Hide(HiddenBy by)
    {
        // Reports don't override a moderator's decision.
        if (HiddenBy == Trails.HiddenBy.Moderator && by != Trails.HiddenBy.Moderator)
        {
            return;
        }

        HiddenBy = by;
    }

    internal void Unhide() => HiddenBy = null;
}
