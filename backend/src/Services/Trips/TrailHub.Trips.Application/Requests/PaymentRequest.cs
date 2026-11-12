namespace TrailHub.Trips.Application.Requests;

/// <summary><paramref name="From"/> paid <paramref name="To"/> this amount in the trip currency.</summary>
public sealed record PaymentRequest(string? From, string? To, decimal Amount, DateOnly Date);
