namespace TrailHub.Trips.Application.Requests;

/// <param name="TentNights">From the trip's planned route, if the app has it; otherwise guessed from the days' overnight names.</param>
public sealed record GearSuggestionsRequest(int? TentNights, int? HutNights, int? HotelNights, double? MaxElevationMeters, int? LongestDayMinutes);
