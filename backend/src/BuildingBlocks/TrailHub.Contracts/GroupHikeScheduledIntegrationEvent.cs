namespace TrailHub.Contracts;

/// <summary>A group hike was scheduled. <paramref name="GroupName"/> and <paramref name="ScheduledById"/> were added later and are null from older publishers.</summary>
public sealed record GroupHikeScheduledIntegrationEvent(
    Guid GroupId,
    Guid HikeId,
    string Title,
    DateTime ScheduledAtUtc,
    IReadOnlyList<string> MemberUserIds,
    string? GroupName = null,
    string? ScheduledById = null);
