using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Activity.Infrastructure.Persistence.Configurations;

namespace TrailHub.Activity.Infrastructure.Persistence;

public sealed class ActivityDbContext(DbContextOptions<ActivityDbContext> options) : DbContext(options), IUnitOfWork
{
    internal const string Path = "Path";
    private static readonly GeometryFactory Geography = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public DbSet<HikeActivity> Activities => Set<HikeActivity>();

    public DbSet<HikeLike> HikeLikes => Set<HikeLike>();

    public DbSet<HikeComment> HikeComments => Set<HikeComment>();

    public DbSet<ContentReport> ContentReports => Set<ContentReport>();

    internal DbSet<KnownGroup> KnownGroups => Set<KnownGroup>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Each segment becomes one line of a MULTILINESTRING, so pauses don't draw a straight jump on the map.
        foreach (var entry in ChangeTracker.Entries<HikeActivity>().Where(e => e.State == EntityState.Added))
        {
            var lines = entry.Entity.Segments
                .Where(s => s.Points.Count >= 2)
                .Select(s => Geography.CreateLineString(s.Points.Select(p => new Coordinate(p.Longitude, p.Latitude)).ToArray()))
                .ToArray();
            entry.Property(Path).CurrentValue = lines.Length > 0 ? Geography.CreateMultiLineString(lines) : null;
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("activity");
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfiguration(new HikeActivityConfiguration());
        modelBuilder.ApplyConfiguration(new HikeLikeConfiguration());
        modelBuilder.ApplyConfiguration(new HikeCommentConfiguration());
        modelBuilder.ApplyConfiguration(new ContentReportConfiguration());
        modelBuilder.ApplyConfiguration(new KnownGroupConfiguration());
    }
}
