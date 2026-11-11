using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Persistence.Configurations;

internal sealed class ConditionReportConfiguration : IEntityTypeConfiguration<ConditionReport>
{
    public void Configure(EntityTypeBuilder<ConditionReport> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Condition).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Note).HasMaxLength(ConditionReport.MaxNoteLength);
        builder.HasIndex(r => new { r.TrailId, r.ReportedAtUtc });
        builder.HasOne<Trail>().WithMany().HasForeignKey(r => r.TrailId).OnDelete(DeleteBehavior.Cascade);
    }
}
