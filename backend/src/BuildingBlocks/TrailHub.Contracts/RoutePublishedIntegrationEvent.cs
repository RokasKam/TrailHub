namespace TrailHub.Contracts;

/// <summary>
/// A user published one of their planned routes to Discover. The Trails service creates (or, when published again,
/// updates) a public trail with the same id as the route.
/// </summary>
/// <param name="Difficulty">Easy, Moderate, Hard or Expert.</param>
/// <param name="Points">The routed line as [latitude, longitude, elevation] triples; elevation may be null.</param>
public sealed record RoutePublishedIntegrationEvent(
    Guid RouteId,
    string OwnerId,
    string Name,
    string? Description,
    string Region,
    string Difficulty,
    IReadOnlyList<double?[]> Points,
    DateTime PublishedAtUtc);
