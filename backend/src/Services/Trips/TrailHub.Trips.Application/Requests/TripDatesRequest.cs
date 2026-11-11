namespace TrailHub.Trips.Application.Requests;

public sealed record TripDatesRequest(DateOnly StartDate, DateOnly EndDate);
