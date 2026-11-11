using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Domain.Activities;

/// <summary>A continuous stretch of recording. Pausing on the device ends a segment; resuming starts a new one.</summary>
public sealed record ActivitySegment(IReadOnlyList<TrackPoint> Points);
