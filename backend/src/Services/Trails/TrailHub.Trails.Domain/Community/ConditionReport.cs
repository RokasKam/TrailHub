using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trails.Domain.Community;

/// <summary>A dated, user-submitted snapshot of on-the-ground conditions.</summary>
public sealed class ConditionReport : AggregateRoot<Guid>
{
    public const int MaxNoteLength = 2000;

    private ConditionReport()
    {
    }

    public Guid TrailId { get; private set; }

    public string UserId { get; private set; } = null!;

    public TrailCondition Condition { get; private set; }

    public string? Note { get; private set; }

    public DateTime ReportedAtUtc { get; private set; }

    public static Result<ConditionReport> Create(Guid trailId, string userId, TrailCondition condition, string? note, DateTime nowUtc)
    {
        if (note?.Length > MaxNoteLength)
        {
            return CommunityErrors.NoteTooLong;
        }

        return new ConditionReport
        {
            Id = Guid.CreateVersion7(),
            TrailId = trailId,
            UserId = userId,
            Condition = condition,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ReportedAtUtc = nowUtc,
        };
    }
}
