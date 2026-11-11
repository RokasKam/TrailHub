using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Activity.Domain.Feed;

/// <summary>
/// Someone flagged a comment or hike photo. One per user per item (reporting again updates and reopens it); a
/// moderator's decision on the item resolves all of its reports, and deleting the item deletes them.
/// </summary>
public sealed class ContentReport : AggregateRoot<Guid>
{
    public const int MaxDetailsLength = 1000;

    /// <summary>Distinct open reports that hide a comment or photo until a moderator looks at it.</summary>
    public const int AutoHideReportCount = 3;

    private ContentReport()
    {
    }

    public ContentKind Kind { get; private set; }

    /// <summary>The hike the comment or photo belongs to.</summary>
    public Guid ActivityId { get; private set; }

    /// <summary>The comment's or photo's id.</summary>
    public Guid ContentId { get; private set; }

    public string UserId { get; private set; } = null!;

    public ContentReportReason Reason { get; private set; }

    public string? Details { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public bool IsOpen => ResolvedAtUtc is null;

    /// <param name="kind">Comment or hike photo.</param>
    /// <param name="activityId">The hike the content belongs to.</param>
    /// <param name="contentId">The comment's or photo's id.</param>
    /// <param name="contentAuthorId">Who wrote the comment or posted the photo; they can't report it.</param>
    /// <param name="userId">The reporter.</param>
    /// <param name="reason">Why.</param>
    /// <param name="details">Optional, at most <see cref="MaxDetailsLength"/> characters.</param>
    /// <param name="nowUtc">The current time.</param>
    public static Result<ContentReport> Create(
        ContentKind kind,
        Guid activityId,
        Guid contentId,
        string contentAuthorId,
        string userId,
        ContentReportReason reason,
        string? details,
        DateTime nowUtc)
    {
        if (contentAuthorId == userId)
        {
            return ActivityErrors.CantReportOwnContent;
        }

        if (Validate(reason, details) is { } error)
        {
            return error;
        }

        return new ContentReport
        {
            Id = Guid.CreateVersion7(),
            Kind = kind,
            ActivityId = activityId,
            ContentId = contentId,
            UserId = userId,
            Reason = reason,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>Whether this many open reports hide the content.</summary>
    public static bool ShouldAutoHide(int openReportCount) => openReportCount >= AutoHideReportCount;

    public Result Update(ContentReportReason reason, string? details, DateTime nowUtc)
    {
        if (Validate(reason, details) is { } error)
        {
            return error;
        }

        Reason = reason;
        Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        CreatedAtUtc = nowUtc;
        ResolvedAtUtc = null; // reporting reviewed content again reopens the report
        return Result.Success();
    }

    public void Resolve(DateTime nowUtc) => ResolvedAtUtc ??= nowUtc;

    private static Error? Validate(ContentReportReason reason, string? details) =>
        !Enum.IsDefined(reason) || details?.Length > MaxDetailsLength ? ActivityErrors.InvalidReport : null;
}
