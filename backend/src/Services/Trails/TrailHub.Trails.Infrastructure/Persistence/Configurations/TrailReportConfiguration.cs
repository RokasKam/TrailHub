using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Persistence.Configurations;

internal sealed class TrailReportConfiguration : IEntityTypeConfiguration<TrailReport>
{
    public void Configure(EntityTypeBuilder<TrailReport> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Details).HasMaxLength(TrailReport.MaxDetailsLength);
        builder.Ignore(r => r.IsOpen);
        builder.HasIndex(r => new { r.TrailId, r.UserId }).IsUnique();
        builder.HasOne<Trail>().WithMany().HasForeignKey(r => r.TrailId).OnDelete(DeleteBehavior.Cascade);
    }
}
