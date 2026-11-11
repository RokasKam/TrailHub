using Microsoft.EntityFrameworkCore;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Profile.Infrastructure.Persistence.Configurations;

namespace TrailHub.Profile.Infrastructure.Persistence;

public sealed class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<UserProfile> Profiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("profile");
        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
    }
}
