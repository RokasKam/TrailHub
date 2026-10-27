using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Persistence;

public sealed class TrailsDbContext(DbContextOptions<TrailsDbContext> options) : DbContext(options), IUnitOfWork
{
    /// <summary>
    /// PostGIS shadow columns. The domain keeps plain <c>GeoPoint</c>s; these are derived on save so
    /// spatial queries (radius, bounding box, distance sort) can use GiST indexes.
    /// </summary>
    internal const string StartLocation = "StartLocation";
    internal const string Path = "Path";

    /// <summary>Plain copies of the start coordinate for cheap list projections (ST_X/ST_Y don't accept geography).</summary>
    internal const string StartLatitude = "StartLatitude";
    internal const string StartLongitude = "StartLongitude";

    private static readonly GeometryFactory Geography = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public DbSet<Trail> Trails => Set<Trail>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<ConditionReport> ConditionReports => Set<ConditionReport>();

    public DbSet<FavoriteTrail> Favorites => Set<FavoriteTrail>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Trail>().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var route = entry.Entity.Route;
            entry.Property(StartLocation).CurrentValue = Geography.CreatePoint(new Coordinate(route[0].Longitude, route[0].Latitude));
            entry.Property(StartLatitude).CurrentValue = route[0].Latitude;
            entry.Property(StartLongitude).CurrentValue = route[0].Longitude;
            entry.Property(Path).CurrentValue = Geography.CreateLineString(route.Select(p => new Coordinate(p.Longitude, p.Latitude)).ToArray());
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("trails");
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrailsDbContext).Assembly);
    }
}
