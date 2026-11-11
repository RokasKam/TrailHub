using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Infrastructure.Persistence.Configurations;

internal sealed class HikeCommentConfiguration : IEntityTypeConfiguration<HikeComment>
{
    public void Configure(EntityTypeBuilder<HikeComment> builder)
    {
        builder.ToTable("HikeComments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.UserId).HasMaxLength(128).IsRequired();
        builder.Property(c => c.Text).HasMaxLength(HikeComment.MaxTextLength).IsRequired();
        builder.HasIndex(c => c.ActivityId);
        builder.HasOne<HikeActivity>().WithMany().HasForeignKey(c => c.ActivityId).OnDelete(DeleteBehavior.Cascade);
    }
}
