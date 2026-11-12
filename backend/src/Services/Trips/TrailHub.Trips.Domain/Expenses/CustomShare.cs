namespace TrailHub.Trips.Domain.Expenses;

/// <summary>A custom split's amount for one person, in the expense's own currency.</summary>
public sealed record CustomShare(string? UserId, decimal Amount);
