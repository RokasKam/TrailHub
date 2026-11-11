using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Domain.Community;

/// <summary>
/// Someone flagged a trail photo. One per user per photo (reporting again updates and reopens it); a moderator's
/// decision on the photo resolves all of its reports, and deleting the photo deletes them.
/// </summary>
public sealed class PhotoReport : AggregateRoot<Guid>
{
    public const int MaxDetailsLength = 1000;

    private PhotoReport()
    {
    }

    public Guid TrailId { get; private set; }

    public Guid PhotoId { get; private set; }

    public string UserId { get; private set; } = null!;

    public ContentReportReason Reason { get; private set; }

    public string? Details { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public bool IsOpen => ResolvedAtUtc is null;

    public static Result<PhotoReport> Create(Guid trailId, TrailPhoto photo, string userId, ContentReportReason reason, string? details, DateTime nowUtc)
    {
        if (photo.UploadedBy == userId)
        {
            return TrailErrors.CantReportOwnContent;
        }

        if (Validate(reason, details) is { } error)
        {
            return error;
        }

        return new PhotoReport
        {
            Id = Guid.CreateVersion7(),
            TrailId = trailId,
            PhotoId = photo.Id,
            UserId = userId,
            Reason = reason,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            CreatedAtUtc = nowUtc,
        };
    }

    public Result Update(ContentReportReason reason, string? details, DateTime nowUtc)
    {
        if (Validate(reason, details) is { } error)
        {
            return error;
        }

        Reason = reason;
        Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        CreatedAtUtc = nowUtc;
        ResolvedAtUtc = null; // reporting a reviewed photo again reopens the report
        return Result.Success();
    }

    public void Resolve(DateTime nowUtc) => ResolvedAtUtc ??= nowUtc;

    private static Error? Validate(ContentReportReason reason, string? details) =>
        !Enum.IsDefined(reason) || details?.Length > MaxDetailsLength ? TrailErrors.InvalidReport : null;
}
