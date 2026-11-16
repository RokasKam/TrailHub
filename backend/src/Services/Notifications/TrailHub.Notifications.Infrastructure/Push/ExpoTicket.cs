namespace TrailHub.Notifications.Infrastructure.Push;

/// <param name="Status"><c>ok</c> (with a receipt <paramref name="Id"/>) or <c>error</c>.</param>
internal sealed record ExpoTicket(string? Status, string? Id, string? Message, ExpoTicketDetails? Details);
