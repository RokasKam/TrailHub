namespace TrailHub.Groups.Application.Requests;

public sealed record ScheduleHikeRequest(string Title, Guid? TrailId, DateTime ScheduledAtUtc, double? MeetingLatitude, double? MeetingLongitude, string? Notes);
