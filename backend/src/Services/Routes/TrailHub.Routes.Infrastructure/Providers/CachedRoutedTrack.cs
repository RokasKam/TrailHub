using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>A routed line as stored in the cache: compact <c>[lat, lon, elevation]</c> triples.</summary>
internal sealed record CachedRoutedTrack(double?[][] Points, byte[] Sac, int[] Waypoints, bool Snapped)
{
    public static CachedRoutedTrack From(RoutedTrack r) => new(
        r.Track.Points.Select(p => new[] { p.Latitude, p.Longitude, p.Elevation }).ToArray(),
        [.. r.Track.SacScale],
        [.. r.WaypointIndices],
        r.Snapped);

    public RoutedTrack ToRoutedTrack() => new(new Track(Points.Select(p => new GeoPoint(p[0]!.Value, p[1]!.Value, p[2])).ToList(), Sac), Waypoints, Snapped);
}
