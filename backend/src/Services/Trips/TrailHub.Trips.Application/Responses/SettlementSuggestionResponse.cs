namespace TrailHub.Trips.Application.Responses;

/// <summary>A payment that helps settle up: <see cref="From"/> pays <see cref="To"/> this amount in the trip currency.</summary>
public sealed record SettlementSuggestionResponse(PersonResponse From, PersonResponse To, decimal Amount);
