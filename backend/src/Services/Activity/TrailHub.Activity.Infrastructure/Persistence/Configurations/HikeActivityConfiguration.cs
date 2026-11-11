using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;

namespace TrailHub.Activity.Infrastructure.Persistence.Configurations;

internal sealed class HikeActivityConfiguration : IEntityTypeConfiguration<HikeActivity>
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
        builder.Property<MultiLineString>(ActivityDbContext.Path).HasColumnType("geography (multilinestring, 4326)");
        builder.HasIndex(ActivityDbContext.Path).HasMethod("gist");
        builder.HasIndex(a => new { a.UserId, a.StartedAtUtc });
        builder.Ignore(a => a.AllPoints);

        // Sharing with groups: "shared with any of my groups" is an array overlap, served by the GIN index.
        builder.Property(a => a.SharedGroupIds); // uuid[]
        builder.HasIndex(a => a.SharedGroupIds).HasMethod("gin");
        builder.Property(a => a.Caption).HasMaxLength(HikeActivity.MaxCaptionLength);
        builder.Property(a => a.Privacy).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => a.SharedAtUtc);
        builder.Property(a => a.Photos).HasJsonConversion();
        builder.Ignore(a => a.IsShared);
    }
}
