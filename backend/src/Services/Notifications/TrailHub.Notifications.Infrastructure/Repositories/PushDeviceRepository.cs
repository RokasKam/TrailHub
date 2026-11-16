using Microsoft.EntityFrameworkCore;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Domain.Devices;
using TrailHub.Notifications.Infrastructure.Persistence;

namespace TrailHub.Notifications.Infrastructure.Repositories;

internal sealed class PushDeviceRepository(NotificationsDbContext db) : IPushDeviceRepository
{
    public Task<PushDevice?> GetByTokenAsync(string token, CancellationToken cancellationToken) =>
        db.PushDevices.FirstOrDefaultAsync(d => d.Token == token, cancellationToken);

    public async Task<IReadOnlyList<PushDevice>> GetForUserAsync(string userId, CancellationToken cancellationToken) =>
        await db.PushDevices.AsNoTracking().Where(d => d.UserId == userId).ToListAsync(cancellationToken);

    public void Add(PushDevice device) => db.PushDevices.Add(device);

    public void Remove(PushDevice device) => db.PushDevices.Remove(device);

    public Task RemoveTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken)
    {
        var list = tokens.ToArray();
        return db.PushDevices.Where(d => list.Contains(d.Token)).ExecuteDeleteAsync(cancellationToken);
    }
}
