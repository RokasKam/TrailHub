namespace TrailHub.Trips.Application.Requests;

/// <param name="Amount">In the expense's currency.</param>
public sealed record CustomShareRequest(string? UserId, decimal Amount);
