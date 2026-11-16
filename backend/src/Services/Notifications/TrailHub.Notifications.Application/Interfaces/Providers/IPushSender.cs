using TrailHub.Notifications.Application.Models;

namespace TrailHub.Notifications.Application.Interfaces.Providers;

/// <summary>Sends pushes to phones (Expo push service). Never throws: failures are logged.</summary>
public interface IPushSender
{
    /// <summary>Sends the messages and returns the tokens the push service says are no longer registered.</summary>
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken);
}
