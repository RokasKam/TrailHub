namespace TrailHub.Activity.Application.Responses;

/// <summary>A group the caller can share a hike with.</summary>
public sealed record ShareTargetResponse(Guid GroupId, string Name, int MemberCount);
