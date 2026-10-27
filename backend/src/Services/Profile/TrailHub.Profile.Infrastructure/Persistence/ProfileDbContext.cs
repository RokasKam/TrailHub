using Microsoft.EntityFrameworkCore;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Profile.Application.Profiles;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Infrastructure.Persistence;

public sealed class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<UserProfile> Profiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("profile");

        var profile = modelBuilder.Entity<UserProfile>();
        profile.ToTable("Profiles");
        profile.HasKey(p => p.Id);
        profile.HasIndex(p => p.UserId).IsUnique();
        profile.Property(p => p.UserId).HasMaxLength(128).IsRequired();
        profile.Property(p => p.DisplayName).HasMaxLength(UserProfile.MaxDisplayNameLength).IsRequired();
        profile.Property(p => p.Email).HasMaxLength(320);
        profile.Property(p => p.Bio).HasMaxLength(UserProfile.MaxBioLength);
        profile.Property(p => p.HomeRegion).HasMaxLength(200);
        profile.Property(p => p.AvatarUrl).HasMaxLength(2048);
        profile.Ignore(p => p.Totals);

        // Concurrent ActivityCompleted events for the same user must not overwrite each other;
        // a conflict throws and the message bus retries the event against fresh state.
        profile.Property<uint>("xmin").IsRowVersion();

        profile.ComplexProperty(p => p.Preferences, pref =>
        {
            pref.Property(x => x.Units).HasConversion<string>().HasMaxLength(20);
            pref.Property(x => x.MapStyle).HasConversion<string>().HasMaxLength(20);
        });

        profile.OwnsMany(p => p.Hikes, hike =>
        {
            hike.ToTable("RecordedHikes");
            hike.WithOwner().HasForeignKey("ProfileId");
            hike.HasKey("ProfileId", nameof(RecordedHike.ActivityId));
        });

        profile.OwnsMany(p => p.Achievements, achievement =>
        {
            achievement.ToTable("UnlockedAchievements");
            achievement.WithOwner().HasForeignKey("ProfileId");
            achievement.HasKey("ProfileId", nameof(UnlockedAchievement.Code));
            achievement.Property(a => a.Code).HasMaxLength(64);
        });
    }
}

internal sealed class ProfileRepository(ProfileDbContext db) : IProfileRepository
{
    public Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public void Add(UserProfile profile) => db.Profiles.Add(profile);
}
