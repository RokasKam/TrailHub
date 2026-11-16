namespace TrailHub.Contracts;

/// <summary>A hike was shared with groups: on the first share, and later only with the groups that were added (<paramref name="GroupIds"/>).</summary>
public sealed record HikeSharedIntegrationEvent(Guid ActivityId, string AuthorId, string AuthorName, string HikeName, IReadOnlyList<Guid> GroupIds);
