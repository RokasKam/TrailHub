namespace TrailHub.Contracts;

/// <summary>
/// A user's display name changed (set on the first sign-in, made readable from an email, or edited). Groups updates
/// the member's name in every group and republishes their member lists, so the other read models follow.
/// </summary>
public sealed record ProfileRenamedIntegrationEvent(string UserId, string DisplayName);
