using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Domain.Devices;

namespace TrailHub.UnitTests;

internal sealed class FakePushDeviceRepository : IPushDeviceRepository
{
    public List<PushDevice> Items { get; } = [];

    public Task<PushDevice?> GetByTokenAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(d => d.Token == token));

    public Task<IReadOnlyList<PushDevice>> GetForUserAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PushDevice>>(Items.Where(d => d.UserId == userId).ToList());

    public void Add(PushDevice device) => Items.Add(device);

    public void Remove(PushDevice device) => Items.Remove(device);

    public Task RemoveTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken)
    {
        Items.RemoveAll(d => tokens.Contains(d.Token));
        return Task.CompletedTask;
    }
}
