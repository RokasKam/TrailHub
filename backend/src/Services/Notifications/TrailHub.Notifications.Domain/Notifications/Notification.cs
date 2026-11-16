using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Notifications.Domain.Notifications;

/// <summary>
/// One item in a user's inbox. Most are written once; likes on a hike are batched into a single item per hike
/// (<see cref="GroupKey"/> <c>like:{activityId}</c>) whose text, count and time update and which becomes unread again.
/// For other kinds, <see cref="GroupKey"/> names the event it came from so a redelivered event isn't stored twice.
/// </summary>
public sealed class Notification : AggregateRoot<Guid>
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 500;
    public const int MaxTargetUrlLength = 300;
    public const int MaxGroupKeyLength = 200;

    /// <summary>At most one push per batched item in this time.</summary>
    public static readonly TimeSpan BatchPushInterval = TimeSpan.FromMinutes(10);

    private List<string> _actorIds = [];

    private Notification()
    {
    }

    public string UserId { get; private set; } = null!;

    public NotificationCategory Category { get; private set; }

    public NotificationKind Kind { get; private set; }

    public string Title { get; private set; } = null!;

    public string Body { get; private set; } = null!;

    /// <summary>The app path to open, e.g. <c>/feed/{activityId}</c>.</summary>
    public string TargetUrl { get; private set; } = null!;

    public string? GroupKey { get; private set; }

    /// <summary>Who caused it (for a batch: everyone, latest last).</summary>
    public IReadOnlyList<string> ActorIds => _actorIds;

    /// <summary>How many people are in the batch (1 for a single event).</summary>
    public int Count { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    /// <summary>When a push last went out for this item; throttles batched items.</summary>
    public DateTime? LastPushedAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    public static NotificationCategory CategoryOf(NotificationKind kind) => kind switch
    {
        NotificationKind.HikeLiked or NotificationKind.HikeCommented => NotificationCategory.LikesComments,
        NotificationKind.HikeShared or NotificationKind.GroupHikeScheduled or NotificationKind.GroupMemberJoined => NotificationCategory.Group,
        _ => NotificationCategory.Trips,
    };

    public static string LikeGroupKey(Guid activityId) => $"like:{activityId}";

    public static Notification Create(
        string userId,
        NotificationKind kind,
        string title,
        string body,
        string targetUrl,
        string? actorId,
        string? groupKey,
        DateTime nowUtc) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            Category = CategoryOf(kind),
            Title = Clip(title, MaxTitleLength),
            Body = Clip(body, MaxBodyLength),
            TargetUrl = Clip(targetUrl, MaxTargetUrlLength),
            GroupKey = groupKey,
            _actorIds = actorId is null ? [] : [actorId],
            Count = 1,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };

    /// <summary>The first like on a hike: "Ben liked your hike Rysy ridge".</summary>
    public static Notification ForLike(string ownerId, Guid activityId, string hikeName, string likerId, string likerName, DateTime nowUtc) =>
        Create(ownerId, NotificationKind.HikeLiked, LikeTitle(1), LikeText(likerName, 1, hikeName), $"/feed/{activityId}", likerId, LikeGroupKey(activityId), nowUtc);

    /// <summary>"Ben liked your hike Rysy ridge" or "Ben and 2 others liked your hike Rysy ridge".</summary>
    public static string LikeText(string latestName, int count, string hikeName) =>
        count <= 1 ? $"{latestName} liked your hike {hikeName}"
        : $"{latestName} and {count - 1} {(count == 2 ? "other" : "others")} liked your hike {hikeName}";

    private static string LikeTitle(int count) => count <= 1 ? "New like" : "New likes";

    /// <summary>
    /// Adds a like to this batch: the text names the latest liker and counts the rest, the item moves to the top and
    /// is unread again. Someone already counted (they unliked and liked again) changes nothing. Returns whether it changed.
    /// </summary>
    public bool AddLike(string likerId, string likerName, string hikeName, DateTime nowUtc)
    {
        if (Kind != NotificationKind.HikeLiked || _actorIds.Contains(likerId))
        {
            return false;
        }

        _actorIds.Add(likerId);
        Count = _actorIds.Count;
        Title = LikeTitle(Count);
        Body = Clip(LikeText(likerName, Count, hikeName), MaxBodyLength);
        UpdatedAtUtc = nowUtc;
        ReadAtUtc = null;
        return true;
    }

    /// <summary>Whether a push may go out now: always for single events; batched likes at most once per <see cref="BatchPushInterval"/>.</summary>
    public bool CanPush(DateTime nowUtc) =>
        Kind != NotificationKind.HikeLiked || LastPushedAtUtc is not { } last || nowUtc - last >= BatchPushInterval;

    public void MarkPushed(DateTime nowUtc) => LastPushedAtUtc = nowUtc;

    public void MarkRead(DateTime nowUtc) => ReadAtUtc ??= nowUtc;

    private static string Clip(string text, int max) => text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
