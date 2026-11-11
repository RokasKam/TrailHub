namespace TrailHub.Routes.Domain.Routes;

/// <summary>The router's answer for a list of waypoints: a line along paths, and where each waypoint landed on it.</summary>
/// <param name="Snapped">False when the line is straight segments between waypoints (no hiking paths found, or no router configured).</param>
public sealed record RoutedTrack(Track Track, IReadOnlyList<int> WaypointIndices, bool Snapped);
