using Microsoft.Extensions.Logging;
using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Models;

namespace TrailHub.Notifications.Infrastructure.Push;

/// <summary>Used when <c>Push:Enabled</c> is false (tests, CI): logs what would have been sent.</summary>
internal sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken)
    {
        foreach (var m in messages)
        {
            logger.LogInformation("Push (disabled) to {Token}: {Title} / {Body} → {Url}", m.To, m.Title, m.Body, m.Url);
        }

        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
