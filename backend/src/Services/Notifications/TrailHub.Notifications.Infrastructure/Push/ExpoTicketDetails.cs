namespace TrailHub.Notifications.Infrastructure.Push;

/// <param name="Error">e.g. <c>DeviceNotRegistered</c>, <c>MessageTooBig</c>, <c>MessageRateExceeded</c>.</param>
/// <param name="ExpoPushToken">The token the error is about, when Expo includes it.</param>
internal sealed record ExpoTicketDetails(string? Error, string? ExpoPushToken);
