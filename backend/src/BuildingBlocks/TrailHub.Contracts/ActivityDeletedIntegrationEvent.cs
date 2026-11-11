namespace TrailHub.Contracts;

public sealed record ActivityDeletedIntegrationEvent(Guid ActivityId, string UserId);
