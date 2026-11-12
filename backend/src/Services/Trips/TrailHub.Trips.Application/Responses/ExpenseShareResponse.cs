namespace TrailHub.Trips.Application.Responses;

/// <param name="Amount">In the trip currency.</param>
public sealed record ExpenseShareResponse(string UserId, string DisplayName, decimal Amount);
