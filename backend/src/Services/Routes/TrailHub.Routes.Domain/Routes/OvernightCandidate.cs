using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Routes.Domain.Routes;

/// <summary>A place to sleep near the route, located on the track.</summary>
/// <param name="TrackIndex">The track point closest to it.</param>
/// <param name="OffTrackMeters">How far it is from the track (walked there and back).</param>
public sealed record OvernightCandidate(string Name, PoiKind Kind, GeoPoint Location, int TrackIndex, double OffTrackMeters);
