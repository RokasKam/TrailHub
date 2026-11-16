namespace TrailHub.Notifications.Infrastructure.Push;

/// <summary>What the app reads when the notification is tapped: the path to open and the inbox item to mark read.</summary>
internal sealed record ExpoMessageData(string Url, Guid NotificationId);
