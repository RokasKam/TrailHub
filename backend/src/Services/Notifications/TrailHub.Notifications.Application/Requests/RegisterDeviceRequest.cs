namespace TrailHub.Notifications.Application.Requests;

/// <param name="Token">The Expo push token, e.g. <c>ExponentPushToken[…]</c>.</param>
/// <param name="Platform"><c>ios</c> or <c>android</c>.</param>
public sealed record RegisterDeviceRequest(string? Token, string? Platform);
