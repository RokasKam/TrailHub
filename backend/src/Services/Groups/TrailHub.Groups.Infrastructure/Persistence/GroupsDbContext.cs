using Microsoft.EntityFrameworkCore;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Groups.Infrastructure.Persistence.Configurations;

// Namespace kept as-is: the migrations reference GroupsDbContext by it.
namespace TrailHub.Groups.Infrastructure;

public sealed class GroupsDbContext(DbContextOptions<GroupsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<HikingGroup> Groups => Set<HikingGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("groups");
        modelBuilder.ApplyConfiguration(new HikingGroupConfiguration());
    }
}
