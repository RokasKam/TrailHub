using Microsoft.EntityFrameworkCore;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Trips.Domain.Gear;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Trips.Infrastructure.Persistence.Configurations;

namespace TrailHub.Trips.Infrastructure.Persistence;

public sealed class TripsDbContext(DbContextOptions<TripsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Trip> Trips => Set<Trip>();

    internal DbSet<KnownGroup> KnownGroups => Set<KnownGroup>();

    public DbSet<GearList> GearLists => Set<GearList>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("trips");
        modelBuilder.ApplyConfiguration(new TripConfiguration());
        modelBuilder.ApplyConfiguration(new GearListConfiguration());
        modelBuilder.ApplyConfiguration(new KnownGroupConfiguration());
    }
}
