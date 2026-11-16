using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> n)
    {
        n.ToTable("Notifications");
        n.HasKey(x => x.Id);
        n.Property(x => x.Id).ValueGeneratedNever();
        n.Property(x => x.UserId).HasMaxLength(128).IsRequired();
        n.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        n.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        n.Property(x => x.Title).HasMaxLength(Notification.MaxTitleLength).IsRequired();
        n.Property(x => x.Body).HasMaxLength(Notification.MaxBodyLength).IsRequired();
        n.Property(x => x.TargetUrl).HasMaxLength(Notification.MaxTargetUrlLength).IsRequired();
        n.Property(x => x.GroupKey).HasMaxLength(Notification.MaxGroupKeyLength);
        n.Property<List<string>>("_actorIds").HasColumnName("ActorIds"); // text[] column
        n.Ignore(x => x.ActorIds);
        n.Ignore(x => x.IsRead);

        // The inbox, newest first; and the unread badge.
        n.HasIndex(x => new { x.UserId, x.UpdatedAtUtc });
        n.HasIndex(x => x.UserId).HasFilter("\"ReadAtUtc\" IS NULL").HasDatabaseName("IX_Notifications_UserId_Unread");

        // One item per key and user: like batches, and no duplicates when an event is delivered twice.
        n.HasIndex(x => new { x.UserId, x.GroupKey }).IsUnique().HasFilter("\"GroupKey\" IS NOT NULL");

        // Likes from several people update the same batch; two at once must not both win.
        n.Property<uint>("xmin").IsRowVersion();
    }
}
