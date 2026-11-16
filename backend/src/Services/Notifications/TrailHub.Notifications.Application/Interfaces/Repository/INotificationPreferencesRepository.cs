using TrailHub.Notifications.Domain.Preferences;

namespace TrailHub.Notifications.Application.Interfaces.Repository;

public interface INotificationPreferencesRepository
{
    /// <summary>The user's stored preferences, or null when they never changed them (all on).</summary>
    Task<NotificationPreferences?> GetAsync(string userId, CancellationToken cancellationToken);

    void Add(NotificationPreferences preferences);
}
