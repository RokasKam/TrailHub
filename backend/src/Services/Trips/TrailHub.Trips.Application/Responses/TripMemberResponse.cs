namespace TrailHub.Trips.Application.Responses;

/// <param name="PersonalDone">How many personal items this member has ticked.</param>
public sealed record TripMemberResponse(string UserId, string DisplayName, bool IsOwner, int PersonalDone);
