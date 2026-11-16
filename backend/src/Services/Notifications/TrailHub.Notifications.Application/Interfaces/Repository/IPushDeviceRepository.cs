using TrailHub.Notifications.Domain.Devices;

namespace TrailHub.Notifications.Application.Interfaces.Repository;

public interface IPushDeviceRepository
{
    Task<PushDevice?> GetByTokenAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<PushDevice>> GetForUserAsync(string userId, CancellationToken cancellationToken);

    void Add(PushDevice device);

    void Remove(PushDevice device);

    /// <summary>Forgets tokens Expo reported as no longer registered (directly in the store).</summary>
    Task RemoveTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken);
}
