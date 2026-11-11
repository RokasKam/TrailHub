using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Persistence.Configurations;

internal sealed class TrailConfiguration : IEntityTypeConfiguration<Trail>
{
    public void Configure(EntityTypeBuilder<Trail> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(Trail.MaxNameLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(Trail.MaxDescriptionLength);
        builder.Property(t => t.Region).HasMaxLength(Trail.MaxRegionLength).IsRequired();
        builder.Property(t => t.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.CreatedBy).HasMaxLength(128).IsRequired();

        // Full-resolution route (with elevation) for rendering and GPX export.
        builder.Property(t => t.Route).HasJsonConversion();

        builder.Property<Point>(TrailsDbContext.StartLocation).HasColumnType("geography (point, 4326)").IsRequired();
        builder.Property<LineString>(TrailsDbContext.Path).HasColumnType("geography (linestring, 4326)").IsRequired();
        builder.Property<double>(TrailsDbContext.StartLatitude);
        builder.Property<double>(TrailsDbContext.StartLongitude);
        builder.HasIndex(TrailsDbContext.StartLocation).HasMethod("gist");
        builder.HasIndex(TrailsDbContext.Path).HasMethod("gist");

        builder.HasIndex(t => t.Name).HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.HasIndex(t => t.Region).HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.HasIndex(t => t.Difficulty);
        builder.HasIndex(t => t.DistanceMeters);

        builder.Ignore(t => t.Start);
        builder.Ignore(t => t.Bounds);
        builder.Ignore(t => t.IsHidden);
        builder.Property(t => t.HiddenBy).HasConversion<string>().HasMaxLength(20);

        builder.OwnsMany(t => t.PointsOfInterest, poi =>
        {
            poi.ToTable("PointsOfInterest");
            poi.WithOwner().HasForeignKey("TrailId");
            poi.HasKey(p => p.Id);
            poi.Property(p => p.Id).ValueGeneratedNever();
            poi.Property(p => p.Name).HasMaxLength(PointOfInterest.MaxNameLength).IsRequired();
            poi.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);
            poi.Property(p => p.Description).HasMaxLength(PointOfInterest.MaxDescriptionLength);
            poi.OwnsOne(p => p.Location, loc =>
            {
                loc.Property(l => l.Latitude).HasColumnName("Latitude");
                loc.Property(l => l.Longitude).HasColumnName("Longitude");
                loc.Property(l => l.Elevation).HasColumnName("Elevation");
            });
            poi.Navigation(p => p.Location).IsRequired();
        });

        builder.OwnsMany(t => t.Photos, photo =>
        {
            photo.ToTable("TrailPhotos");
            photo.WithOwner().HasForeignKey("TrailId");
            photo.HasKey(p => p.Id);
            photo.Property(p => p.Id).ValueGeneratedNever();
            photo.Property(p => p.Url).HasMaxLength(2048).IsRequired();
            photo.Property(p => p.Caption).HasMaxLength(500);
            photo.Property(p => p.UploadedBy).HasMaxLength(128).IsRequired();
            photo.Property(p => p.HiddenBy).HasConversion<string>().HasMaxLength(20);
            photo.Ignore(p => p.IsHidden);
            photo.Ignore(p => p.IsStored);
        });
    }
}
