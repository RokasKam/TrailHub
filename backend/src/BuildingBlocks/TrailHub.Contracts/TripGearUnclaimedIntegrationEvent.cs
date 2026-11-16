namespace TrailHub.Contracts;

/// <summary>A group trip starts in two days and some group gear is still unclaimed. Sent at most once per trip.</summary>
public sealed record TripGearUnclaimedIntegrationEvent(
    Guid TripId,
    string TripName,
    Guid GroupId,
    int UnclaimedCount,
    DateOnly StartDate,
    IReadOnlyList<string> MemberIds);
