namespace TrailHub.Notifications.Application.Models;

/// <summary>One push to one device. <paramref name="Url"/> is the app path to open; <paramref name="Badge"/> the unread count.</summary>
public sealed record PushMessage(string To, string Title, string Body, string Url, Guid NotificationId, int Badge);
