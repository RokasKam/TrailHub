using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Gear;

/// <summary>What is known about a trip when suggesting gear. Route facts come from the app, which has the route.</summary>
/// <param name="TentNights">Nights at campsites or wild camping.</param>
/// <param name="HutNights">Nights in mountain huts or shelters.</param>
/// <param name="HotelNights">Nights in hotels, guesthouses or hostels.</param>
/// <param name="MaxElevationMeters">Highest point of the route, if known.</param>
/// <param name="LongestDayMinutes">Walking time of the longest day, if known.</param>
/// <param name="Shared">Whether the trip is shared with a group (so group gear makes sense).</param>
public sealed record TripGearFacts(
    int Days,
    int TentNights,
    int HutNights,
    int HotelNights,
    double? MaxElevationMeters,
    int? LongestDayMinutes,
    bool Shared);
