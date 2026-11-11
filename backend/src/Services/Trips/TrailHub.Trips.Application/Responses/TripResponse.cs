using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Responses;

/// <param name="Members">The linked group's members with their packing progress; empty for private trips.</param>
public sealed record TripResponse(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    int DayCount,
    TripStatus Status,
    IReadOnlyList<TripDayResponse> Days,
    IReadOnlyList<ChecklistItemResponse> Checklist,
    DateTime CreatedAtUtc,
    string OwnerId,
    bool IsOwner,
    Guid? GroupId,
    string? GroupName,
    IReadOnlyList<TripMemberResponse> Members,
    Guid? RouteId);
