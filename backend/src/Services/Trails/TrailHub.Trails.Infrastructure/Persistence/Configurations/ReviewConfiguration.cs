using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(Review.MaxCommentLength);
        builder.HasIndex(r => new { r.TrailId, r.UserId }).IsUnique();
        builder.HasOne<Trail>().WithMany().HasForeignKey(r => r.TrailId).OnDelete(DeleteBehavior.Cascade);
    }
}
