using Microsoft.EntityFrameworkCore;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Notifications.Domain.Devices;
using TrailHub.Notifications.Domain.Notifications;
using TrailHub.Notifications.Domain.Preferences;
using TrailHub.Notifications.Infrastructure.Persistence.Configurations;

namespace TrailHub.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

    public DbSet<NotificationPreferences> Preferences => Set<NotificationPreferences>();

    internal DbSet<KnownGroup> KnownGroups => Set<KnownGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new PushDeviceConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationPreferencesConfiguration());
        modelBuilder.ApplyConfiguration(new KnownGroupConfiguration());
    }
}
