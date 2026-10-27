using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Trips.Application.Trips;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Infrastructure;

public sealed class TripsDbContext(DbContextOptions<TripsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Trip> Trips => Set<Trip>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("trips");

        var trip = modelBuilder.Entity<Trip>();
        trip.HasKey(t => t.Id);
        trip.Property(t => t.OwnerId).HasMaxLength(128).IsRequired();
        trip.Property(t => t.Name).HasMaxLength(Trip.MaxNameLength).IsRequired();
        trip.Property(t => t.Description).HasMaxLength(Trip.MaxTextLength);
        trip.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        trip.HasIndex(t => new { t.OwnerId, t.StartDate });
        trip.Ignore(t => t.DayCount);
        trip.Property<uint>("xmin").IsRowVersion();

        trip.OwnsMany(t => t.Days, day =>
        {
            day.ToTable("TripDays");
            day.WithOwner().HasForeignKey("TripId");
            day.HasKey("TripId", nameof(TripDay.DayNumber));
            day.Property(d => d.Notes).HasMaxLength(Trip.MaxTextLength);
            day.Property(d => d.Overnight).HasMaxLength(Trip.MaxNameLength);
            day.Property(d => d.TrailIds); // uuid[] column
        });

        trip.OwnsMany(t => t.Checklist, item =>
        {
            item.ToTable("ChecklistItems");
            item.WithOwner().HasForeignKey("TripId");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();
            item.Property(i => i.Text).HasMaxLength(Trip.MaxNameLength).IsRequired();
        });
    }
}

internal sealed class TripRepository(TripsDbContext db) : ITripRepository
{
    public Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Trips.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        await db.Trips.AsNoTracking().Where(t => t.OwnerId == ownerId).OrderBy(t => t.StartDate).ToListAsync(cancellationToken);

    public void Add(Trip trip) => db.Trips.Add(trip);

    public void Remove(Trip trip) => db.Trips.Remove(trip);
}

public static class DependencyInjection
{
    public static IServiceCollection AddTripsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TripsDb")
            ?? throw new InvalidOperationException("Connection string 'TripsDb' is not configured.");

        services.AddDbContext<TripsDbContext>(o => o.UseTripsNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TripsDbContext>());
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddHealthChecks().AddDbContextCheck<TripsDbContext>("trips-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateTripsDatabaseAsync(this IServiceProvider services) =>
        services.MigrateDatabaseAsync<TripsDbContext>();

    internal static DbContextOptionsBuilder UseTripsNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trips"));
}

internal sealed class TripsDbContextFactory : IDesignTimeDbContextFactory<TripsDbContext>
{
    public TripsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TripsDbContext>();
        options.UseTripsNpgsql("Host=localhost;Database=trailhub_trips;Username=trailhub;Password=trailhub");
        return new TripsDbContext(options.Options);
    }
}
