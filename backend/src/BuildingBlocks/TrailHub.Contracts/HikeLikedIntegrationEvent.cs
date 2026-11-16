namespace TrailHub.Contracts;

/// <summary>Someone liked a shared hike (a new like, not a repeat of one they already have). Notifications tells the owner.</summary>
public sealed record HikeLikedIntegrationEvent(Guid ActivityId, string OwnerId, string HikeName, string LikerId, string LikerName);
