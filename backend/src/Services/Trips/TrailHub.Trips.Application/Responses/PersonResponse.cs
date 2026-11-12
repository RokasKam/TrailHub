namespace TrailHub.Trips.Application.Responses;

/// <param name="DisplayName">Their current name, the last one they had on the trip, or "Former member".</param>
public sealed record PersonResponse(string UserId, string DisplayName);
