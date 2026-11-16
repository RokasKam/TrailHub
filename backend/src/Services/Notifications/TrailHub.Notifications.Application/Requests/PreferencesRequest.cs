namespace TrailHub.Notifications.Application.Requests;

/// <summary>Push on/off per category.</summary>
public sealed record PreferencesRequest(bool LikesComments, bool Group, bool Trips);
