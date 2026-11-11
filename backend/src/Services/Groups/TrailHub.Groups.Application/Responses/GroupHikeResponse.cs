using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Responses;

public sealed record GroupHikeResponse(
    Guid Id,
    string Title,
    Guid? TrailId,
    DateTime ScheduledAtUtc,
    double? MeetingLatitude,
    double? MeetingLongitude,
    string? Notes,
    string CreatedBy,
    GroupHikeStatus Status,
    IReadOnlyList<RsvpResponse> Rsvps);
