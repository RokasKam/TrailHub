using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Domain.Preferences;

namespace TrailHub.UnitTests;

internal sealed class FakePreferencesRepository : INotificationPreferencesRepository
{
    public List<NotificationPreferences> Items { get; } = [];

    public Task<NotificationPreferences?> GetAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(p => p.UserId == userId));

    public void Add(NotificationPreferences preferences) => Items.Add(preferences);
}
