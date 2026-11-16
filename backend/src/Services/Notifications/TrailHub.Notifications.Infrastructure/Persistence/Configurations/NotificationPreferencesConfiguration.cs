using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Notifications.Domain.Preferences;

namespace TrailHub.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class NotificationPreferencesConfiguration : IEntityTypeConfiguration<NotificationPreferences>
{
    public void Configure(EntityTypeBuilder<NotificationPreferences> p)
    {
        p.ToTable("NotificationPreferences");
        p.HasKey(x => x.Id);
        p.Property(x => x.Id).HasColumnName("UserId").HasMaxLength(128).ValueGeneratedNever();
        p.Ignore(x => x.UserId);
    }
}
