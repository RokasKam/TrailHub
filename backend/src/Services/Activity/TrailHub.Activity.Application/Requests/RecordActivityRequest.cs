using TrailHub.Activity.Application.Dtos;

namespace TrailHub.Activity.Application.Requests;

/// <param name="Name">Hike name.</param>
/// <param name="TrailId">The trail this hike followed, if any.</param>
/// <param name="Segments">Track points per recording segment (a pause ends a segment).</param>
/// <param name="Notes">Optional notes.</param>
/// <param name="RouteId">The planned route this hike followed, if any.</param>
public sealed record RecordActivityRequest(string Name, Guid? TrailId, IReadOnlyList<IReadOnlyList<TrackPointDto>> Segments, string? Notes, Guid? RouteId = null);
