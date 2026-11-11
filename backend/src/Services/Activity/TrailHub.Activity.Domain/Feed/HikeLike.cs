namespace TrailHub.Activity.Domain.Feed;

/// <summary>A member's like on a shared hike. One per user per hike.</summary>
public sealed class HikeLike
{
    private HikeLike()
    {
    }

    public Guid ActivityId { get; private set; }

    public string UserId { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public static HikeLike Create(Guid activityId, string userId, DateTime nowUtc) =>
        new() { ActivityId = activityId, UserId = userId, CreatedAtUtc = nowUtc };
}
