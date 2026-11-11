using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Persistence.Configurations;

internal sealed class PhotoReportConfiguration : IEntityTypeConfiguration<PhotoReport>
{
    public void Configure(EntityTypeBuilder<PhotoReport> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Details).HasMaxLength(PhotoReport.MaxDetailsLength);
        builder.Ignore(r => r.IsOpen);
        builder.HasIndex(r => new { r.PhotoId, r.UserId }).IsUnique();
        builder.HasIndex(r => r.TrailId);

        // Photos are owned by their trail, so the report can't reference the photo row; the service deletes a photo's
        // reports with it.
        builder.HasOne<Trail>().WithMany().HasForeignKey(r => r.TrailId).OnDelete(DeleteBehavior.Cascade);
    }
}
