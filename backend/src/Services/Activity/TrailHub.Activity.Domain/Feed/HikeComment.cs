using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Activity.Domain.Feed;

/// <summary>A comment on a shared hike, by a member who can see it.</summary>
public sealed class HikeComment : AggregateRoot<Guid>
{
    public const int MaxTextLength = 1000;

    private HikeComment()
    {
    }

    public Guid ActivityId { get; private set; }

    public string UserId { get; private set; } = null!;

    public string Text { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Set while the comment is hidden; hidden comments are only shown to their writer and moderators.</summary>
    public HiddenBy? HiddenBy { get; private set; }

    public bool IsHidden => HiddenBy is not null;

    /// <summary>Creates a comment; the text is trimmed and must then be 1 to <see cref="MaxTextLength"/> characters.</summary>
    public static Result<HikeComment> Create(Guid activityId, string userId, string? text, DateTime nowUtc)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxTextLength)
        {
            return ActivityErrors.InvalidComment;
        }

        return new HikeComment
        {
            Id = Guid.CreateVersion7(),
            ActivityId = activityId,
            UserId = userId,
            Text = trimmed,
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>Commenters can delete their own comments; the hike's author and moderators can delete any comment on it.</summary>
    public bool CanBeDeletedBy(string userId, string hikeAuthorId, bool isModerator = false) =>
        userId == UserId || userId == hikeAuthorId || isModerator;

    /// <summary>Anyone but the writer can report a comment.</summary>
    public bool CanBeReportedBy(string userId) => userId != UserId;

    /// <summary>Hidden comments exist only for their writer and moderators.</summary>
    public bool IsVisibleTo(string userId, bool isModerator) => !IsHidden || userId == UserId || isModerator;

    /// <summary>Hides the comment once it has enough open reports. Returns whether it was hidden now.</summary>
    public bool ApplyReports(int openReportCount)
    {
        if (IsHidden || !ContentReport.ShouldAutoHide(openReportCount))
        {
            return false;
        }

        HiddenBy = Feed.HiddenBy.Reports;
        return true;
    }

    /// <summary>A moderator hides the comment or restores it.</summary>
    public void Moderate(bool hidden) => HiddenBy = hidden ? HiddenByRules.Hide(HiddenBy, Feed.HiddenBy.Moderator) : null;
}
