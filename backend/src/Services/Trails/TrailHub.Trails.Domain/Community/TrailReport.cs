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

/// <summary>
/// Someone flagged a trail as wrong, dangerous or inappropriate. One per user per trail (reporting again updates it);
/// a moderator's decision on the trail resolves all of its reports.
/// </summary>
public sealed class TrailReport : AggregateRoot<Guid>
{
    public const int MaxDetailsLength = 1000;

    private TrailReport()
    {
    }

    public Guid TrailId { get; private set; }

    public string UserId { get; private set; } = null!;

    public ReportReason Reason { get; private set; }

    public string? Details { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public bool IsOpen => ResolvedAtUtc is null;

    public static Result<TrailReport> Create(Trail trail, string userId, ReportReason reason, string? details, DateTime nowUtc)
    {
        if (trail.CreatedBy == userId)
        {
            return TrailErrors.CantReportOwnTrail;
        }

        if (details?.Length > MaxDetailsLength)
        {
            return TrailErrors.InvalidReport;
        }

        return new TrailReport
        {
            Id = Guid.CreateVersion7(),
            TrailId = trail.Id,
            UserId = userId,
            Reason = reason,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            CreatedAtUtc = nowUtc,
        };
    }

    public Result Update(ReportReason reason, string? details, DateTime nowUtc)
    {
        if (details?.Length > MaxDetailsLength)
        {
            return TrailErrors.InvalidReport;
        }

        Reason = reason;
        Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        CreatedAtUtc = nowUtc;
        ResolvedAtUtc = null; // reporting a reviewed trail again reopens the report
        return Result.Success();
    }

    public void Resolve(DateTime nowUtc) => ResolvedAtUtc ??= nowUtc;
}
