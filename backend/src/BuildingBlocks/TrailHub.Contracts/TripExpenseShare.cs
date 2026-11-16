namespace TrailHub.Contracts;

/// <summary>One person's part of a trip expense, in the trip currency.</summary>
public sealed record TripExpenseShare(string UserId, decimal Amount);
