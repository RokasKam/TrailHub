namespace TrailHub.Notifications.Infrastructure.Push;

/// <summary>The push API's reply: one ticket per message, in the order sent, or request-level errors.</summary>
internal sealed record ExpoSendResponse(IReadOnlyList<ExpoTicket>? Data, IReadOnlyList<ExpoError>? Errors);
