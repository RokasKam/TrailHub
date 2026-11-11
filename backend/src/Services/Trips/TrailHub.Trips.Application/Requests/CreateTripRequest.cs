namespace TrailHub.Trips.Application.Requests;

public sealed record CreateTripRequest(string Name, string? Description, DateOnly StartDate, DateOnly EndDate, Guid? GroupId = null);
