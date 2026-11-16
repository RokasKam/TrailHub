namespace TrailHub.Notifications.Infrastructure.Push;

/// <summary>One message in a request to the Expo push API (camelCase on the wire).</summary>
internal sealed record ExpoMessage(string To, string Title, string Body, ExpoMessageData Data, string Sound, int Badge, string ChannelId);
