using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Infrastructure.Persistence.Configurations;

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.ToTable("ContentReports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.UserId).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Details).HasMaxLength(ContentReport.MaxDetailsLength);
        builder.Ignore(r => r.IsOpen);
        builder.HasIndex(r => new { r.ContentId, r.UserId }).IsUnique();
        builder.HasIndex(r => r.ActivityId);

        // Reports go with their hike. Comments and photos are deleted by the services, which delete their reports too.
        builder.HasOne<HikeActivity>().WithMany().HasForeignKey(r => r.ActivityId).OnDelete(DeleteBehavior.Cascade);
    }
}
