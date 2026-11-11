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

    /// <summary>Commenters can delete their own comments; the hike's author can delete any comment on it.</summary>
    public bool CanBeDeletedBy(string userId, string hikeAuthorId) => userId == UserId || userId == hikeAuthorId;
}
