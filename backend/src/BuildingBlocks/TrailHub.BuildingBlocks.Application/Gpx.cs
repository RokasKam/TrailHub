using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.BuildingBlocks.Application.Gpx;

public sealed record GpxTrackPoint(double Latitude, double Longitude, double? Elevation, DateTime? TimeUtc);

public sealed record GpxWaypoint(string? Name, string? Type, string? Description, double Latitude, double Longitude, double? Elevation);

/// <summary>
/// A format-neutral view of a GPX file. A track can have several segments (GPS gaps or
/// pause/resume); routes (<c>&lt;rte&gt;</c>) are read as a single segment.
/// </summary>
public sealed record GpxDocument(
    string? Name,
    string? Description,
    IReadOnlyList<IReadOnlyList<GpxTrackPoint>> Segments,
    IReadOnlyList<GpxWaypoint> Waypoints)
{
    public IEnumerable<GpxTrackPoint> AllPoints => Segments.SelectMany(s => s);
}

public interface IGpxSerializer
{
    Result<GpxDocument> Parse(Stream gpx);

    string Write(GpxDocument document);
}
