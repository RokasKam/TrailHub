namespace TrailHub.Contracts;

/// <summary>The author took a published route out of Discover. The Trails service hides the trail with the route's id.</summary>
public sealed record RouteUnpublishedIntegrationEvent(Guid RouteId, string OwnerId, DateTime UnpublishedAtUtc);
