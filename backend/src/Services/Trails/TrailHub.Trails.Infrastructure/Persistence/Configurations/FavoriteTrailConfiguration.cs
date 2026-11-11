using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Infrastructure.Persistence.Configurations;

internal sealed class FavoriteTrailConfiguration : IEntityTypeConfiguration<FavoriteTrail>
{
    public void Configure(EntityTypeBuilder<FavoriteTrail> builder)
    {
        builder.HasKey(f => new { f.UserId, f.TrailId });
        builder.Property(f => f.UserId).HasMaxLength(128);
        builder.HasOne<Trail>().WithMany().HasForeignKey(f => f.TrailId).OnDelete(DeleteBehavior.Cascade);
    }
}
