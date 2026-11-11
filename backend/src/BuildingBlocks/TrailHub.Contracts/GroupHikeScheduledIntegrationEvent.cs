namespace TrailHub.Contracts;

public sealed record GroupHikeScheduledIntegrationEvent(
    Guid GroupId,
    Guid HikeId,
    string Title,
    DateTime ScheduledAtUtc,
    IReadOnlyList<string> MemberUserIds);
