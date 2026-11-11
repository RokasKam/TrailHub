using Microsoft.AspNetCore.SignalR;

namespace TrailHub.Groups.Api.Hubs;

/// <summary>SignalR user id = Auth0 subject, so <c>Clients.User(sub)</c> addresses every device of a person.</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirst("sub")?.Value;
}
