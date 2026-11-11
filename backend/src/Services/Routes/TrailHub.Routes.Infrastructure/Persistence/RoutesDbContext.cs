using Microsoft.EntityFrameworkCore;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Routes.Domain.Routes;
using TrailHub.Routes.Infrastructure.Persistence.Configurations;

namespace TrailHub.Routes.Infrastructure;

// Namespace kept as it was: the migrations refer to this context through it.
public sealed class RoutesDbContext(DbContextOptions<RoutesDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<PlannedRoute> Routes => Set<PlannedRoute>();

    internal DbSet<KnownGroup> KnownGroups => Set<KnownGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("routes");
        modelBuilder.ApplyConfiguration(new PlannedRouteConfiguration());
        modelBuilder.ApplyConfiguration(new KnownGroupConfiguration());
    }
}
