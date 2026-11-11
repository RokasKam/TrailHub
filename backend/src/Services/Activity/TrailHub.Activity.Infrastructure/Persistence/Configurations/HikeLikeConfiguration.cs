using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Infrastructure.Persistence.Configurations;

internal sealed class HikeLikeConfiguration : IEntityTypeConfiguration<HikeLike>
{
    public void Configure(EntityTypeBuilder<HikeLike> builder)
    {
        builder.ToTable("HikeLikes");
        builder.HasKey(l => new { l.ActivityId, l.UserId }); // also serves "likes of this hike" lookups
        builder.Property(l => l.UserId).HasMaxLength(128);
        builder.HasOne<HikeActivity>().WithMany().HasForeignKey(l => l.ActivityId).OnDelete(DeleteBehavior.Cascade);
    }
}
