using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Responses;

/// <param name="SharedUnclaimed">Group gear nobody has said they'll bring yet.</param>
public sealed record TripSummaryResponse(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int DayCount,
    TripStatus Status,
    int TrailCount,
    Guid? GroupId,
    string? GroupName,
    bool IsOwner,
    int SharedUnclaimed);
