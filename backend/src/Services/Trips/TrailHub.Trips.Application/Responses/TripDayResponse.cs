namespace TrailHub.Trips.Application.Responses;

public sealed record TripDayResponse(int DayNumber, DateOnly Date, string? Notes, string? Overnight, IReadOnlyList<Guid> TrailIds);
