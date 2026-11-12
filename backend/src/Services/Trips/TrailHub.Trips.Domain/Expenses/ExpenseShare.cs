namespace TrailHub.Trips.Domain.Expenses;

/// <summary>One person's part of an expense, in the trip currency.</summary>
public sealed record ExpenseShare(string UserId, decimal Amount);
