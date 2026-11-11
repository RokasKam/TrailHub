using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrailHub.Activity.Infrastructure.Persistence.Configurations;

internal sealed class KnownGroupConfiguration : IEntityTypeConfiguration<KnownGroup>
{
    public void Configure(EntityTypeBuilder<KnownGroup> group)
    {
        group.ToTable("KnownGroups");
        group.HasKey(g => g.Id);
        group.Property(g => g.Id).ValueGeneratedNever();
        group.Property(g => g.Name).HasMaxLength(100).IsRequired();
        group.OwnsMany(g => g.Members, member =>
        {
            member.ToTable("KnownGroupMembers");
            member.WithOwner().HasForeignKey("GroupId");
            member.HasKey("GroupId", nameof(KnownGroupMember.UserId));
            member.Property(m => m.UserId).HasMaxLength(128);
            member.Property(m => m.DisplayName).HasMaxLength(100).IsRequired();
            member.HasIndex(m => m.UserId);
        });
    }
}
