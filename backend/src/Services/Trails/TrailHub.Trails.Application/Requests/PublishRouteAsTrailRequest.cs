using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Requests;

/// <summary>A planned route its author published from the Routes service.</summary>
public sealed record PublishRouteAsTrailRequest(
    Guid RouteId,
    string OwnerId,
    string Name,
    string? Description,
    string Region,
    Difficulty Difficulty,
    IReadOnlyList<double?[]> Points,
    DateTime PublishedAtUtc);
