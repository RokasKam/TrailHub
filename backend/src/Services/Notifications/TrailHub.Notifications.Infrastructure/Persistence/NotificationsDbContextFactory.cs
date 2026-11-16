using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrailHub.Notifications.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> create the context at design time (migrations).</summary>
internal sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>();
        options.UseNotificationsNpgsql("Host=localhost;Database=trailhub_notifications;Username=trailhub;Password=trailhub");
        return new NotificationsDbContext(options.Options);
    }
}
