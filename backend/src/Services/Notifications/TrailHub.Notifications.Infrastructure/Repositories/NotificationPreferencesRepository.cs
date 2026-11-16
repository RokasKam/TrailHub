using Microsoft.EntityFrameworkCore;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Domain.Preferences;
using TrailHub.Notifications.Infrastructure.Persistence;

namespace TrailHub.Notifications.Infrastructure.Repositories;

internal sealed class NotificationPreferencesRepository(NotificationsDbContext db) : INotificationPreferencesRepository
{
    public Task<NotificationPreferences?> GetAsync(string userId, CancellationToken cancellationToken) =>
        db.Preferences.FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

    public void Add(NotificationPreferences preferences) => db.Preferences.Add(preferences);
}
