using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Infrastructure.Persistence.Configurations;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("Profiles");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.UserId).IsUnique();
        builder.Property(p => p.UserId).HasMaxLength(128).IsRequired();
        builder.Property(p => p.DisplayName).HasMaxLength(UserProfile.MaxDisplayNameLength).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(320);
        builder.Property(p => p.Bio).HasMaxLength(UserProfile.MaxBioLength);
        builder.Property(p => p.HomeRegion).HasMaxLength(200);
        builder.Property(p => p.AvatarUrl).HasMaxLength(2048);
        builder.Ignore(p => p.Totals);

        // Concurrent ActivityCompleted events for the same user must not overwrite each other;
        // a conflict throws and the message bus retries the event against fresh state.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.ComplexProperty(p => p.Preferences, pref =>
        {
            pref.Property(x => x.Units).HasConversion<string>().HasMaxLength(20);
            pref.Property(x => x.MapStyle).HasConversion<string>().HasMaxLength(20);
        });

        builder.OwnsMany(p => p.Hikes, hike =>
        {
            hike.ToTable("RecordedHikes");
            hike.WithOwner().HasForeignKey("ProfileId");
            hike.HasKey("ProfileId", nameof(RecordedHike.ActivityId));
        });

        builder.OwnsMany(p => p.Achievements, achievement =>
        {
            achievement.ToTable("UnlockedAchievements");
            achievement.WithOwner().HasForeignKey("ProfileId");
            achievement.HasKey("ProfileId", nameof(UnlockedAchievement.Code));
            achievement.Property(a => a.Code).HasMaxLength(64);
        });
    }
}
