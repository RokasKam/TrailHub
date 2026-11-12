namespace TrailHub.Trips.Domain.Expenses;

/// <summary>A settle-up payment as entered: <paramref name="From"/> paid <paramref name="To"/> this amount in the trip currency.</summary>
public sealed record PaymentDetails(string? From, string? To, decimal Amount, DateOnly Date);
