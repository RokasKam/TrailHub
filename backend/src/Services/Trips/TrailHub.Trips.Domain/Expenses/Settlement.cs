namespace TrailHub.Trips.Domain.Expenses;

/// <summary>A suggested payment: <see cref="From"/> pays <see cref="To"/> this amount in the trip currency.</summary>
public sealed record Settlement(string From, string To, decimal Amount);
