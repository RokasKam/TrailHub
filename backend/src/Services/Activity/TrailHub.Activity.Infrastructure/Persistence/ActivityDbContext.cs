using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using TrailHub.Activity.Application.Activities;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;

namespace TrailHub.Activity.Infrastructure.Persistence;

public sealed class ActivityDbContext(DbContextOptions<ActivityDbContext> options) : DbContext(options), IUnitOfWork
{
    internal const string Path = "Path";
    private static readonly GeometryFactory Geography = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public DbSet<HikeActivity> Activities => Set<HikeActivity>();

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
    }

    private sealed class HikeActivityConfiguration : IEntityTypeConfiguration<HikeActivity>
    {
        public void Configure(EntityTypeBuilder<HikeActivity> builder)
        {
            builder.ToTable("Activities");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.UserId).HasMaxLength(128).IsRequired();
            builder.Property(a => a.Name).HasMaxLength(HikeActivity.MaxNameLength).IsRequired();
            builder.Property(a => a.Notes).HasMaxLength(HikeActivity.MaxNotesLength);
            builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.Segments).HasJsonConversion();
            builder.Property<MultiLineString>(Path).HasColumnType("geography (multilinestring, 4326)");
            builder.HasIndex(Path).HasMethod("gist");
            builder.HasIndex(a => new { a.UserId, a.StartedAtUtc });
            builder.Ignore(a => a.AllPoints);
        }
    }
}

internal sealed class ActivityRepository(ActivityDbContext db) : IActivityRepository
{
    public Task<HikeActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Activities.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Add(HikeActivity activity) => db.Activities.Add(activity);

    public void Remove(HikeActivity activity) => db.Activities.Remove(activity);

    public async Task<PagedResult<ActivitySummaryDto>> GetPageAsync(string userId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Activities.AsNoTracking().Where(a => a.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.StartedAtUtc)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(a => new ActivitySummaryDto(a.Id, a.Name, a.TrailId, a.Source, a.StartedAtUtc, a.EndedAtUtc, a.DistanceMeters, a.ElevationGainMeters, a.MovingDurationSeconds, a.AverageSpeedMetersPerSecond))
            .ToListAsync(cancellationToken);
        return new PagedResult<ActivitySummaryDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<ActivityTotals> GetTotalsAsync(string userId, DateTime? sinceUtc, CancellationToken cancellationToken)
    {
        var query = db.Activities.Where(a => a.UserId == userId);
        if (sinceUtc is { } since)
        {
            query = query.Where(a => a.StartedAtUtc >= since);
        }

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new ActivityTotals(
                g.Count(),
                g.Sum(a => a.DistanceMeters),
                g.Sum(a => a.ElevationGainMeters),
                g.Sum(a => a.MovingDurationSeconds),
                g.Max(a => a.DistanceMeters),
                g.Max(a => a.MaxElevationMeters)))
            .FirstOrDefaultAsync(cancellationToken);
        return totals ?? new ActivityTotals(0, 0, 0, 0, 0, null);
    }

    public async Task<IReadOnlyList<MonthlyStatisticsDto>> GetMonthlyAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken) =>
        await db.Activities
            .Where(a => a.UserId == userId && a.StartedAtUtc >= sinceUtc)
            .GroupBy(a => new { a.StartedAtUtc.Year, a.StartedAtUtc.Month })
            .Select(g => new MonthlyStatisticsDto(g.Key.Year, g.Key.Month, g.Count(), g.Sum(a => a.DistanceMeters), g.Sum(a => a.ElevationGainMeters)))
            .ToListAsync(cancellationToken);
}
