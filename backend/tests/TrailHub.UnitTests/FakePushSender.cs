using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Models;

namespace TrailHub.UnitTests;

/// <summary>Records pushes; tokens in <see cref="Unregistered"/> are reported as gone.</summary>
internal sealed class FakePushSender : IPushSender
{
    public List<PushMessage> Sent { get; } = [];

    public HashSet<string> Unregistered { get; } = [];

    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken)
    {
        Sent.AddRange(messages);
        return Task.FromResult<IReadOnlyList<string>>(messages.Select(m => m.To).Where(Unregistered.Contains).Distinct().ToList());
    }
}
