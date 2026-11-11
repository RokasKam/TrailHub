using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Infrastructure.Persistence.Configurations;

internal sealed class HikingGroupConfiguration : IEntityTypeConfiguration<HikingGroup>
{
    public void Configure(EntityTypeBuilder<HikingGroup> group)
    {
        group.HasKey(g => g.Id);
        group.Property(g => g.Name).HasMaxLength(HikingGroup.MaxNameLength).IsRequired();
        group.Property(g => g.Description).HasMaxLength(HikingGroup.MaxTextLength);
        group.Property(g => g.InviteCode).HasMaxLength(HikingGroup.InviteCodeLength).IsRequired();
        group.HasIndex(g => g.InviteCode).IsUnique();
        group.Ignore(g => g.OwnerId);
        group.Property<uint>("xmin").IsRowVersion();

        group.OwnsMany(g => g.Members, member =>
        {
            member.ToTable("GroupMembers");
            member.WithOwner().HasForeignKey("GroupId");
            member.HasKey("GroupId", nameof(GroupMember.UserId));
            member.Property(m => m.UserId).HasMaxLength(128);
            member.Property(m => m.DisplayName).HasMaxLength(80).IsRequired();
            member.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            member.HasIndex(m => m.UserId);
        });

        group.OwnsMany(g => g.Hikes, hike =>
        {
            hike.ToTable("GroupHikes");
            hike.WithOwner().HasForeignKey("GroupId");
            hike.HasKey(h => h.Id);
            hike.Property(h => h.Id).ValueGeneratedNever();
            hike.Property(h => h.Title).HasMaxLength(HikingGroup.MaxNameLength).IsRequired();
            hike.Property(h => h.Notes).HasMaxLength(HikingGroup.MaxTextLength);
            hike.Property(h => h.CreatedBy).HasMaxLength(128).IsRequired();
            hike.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
            hike.OwnsMany(h => h.Rsvps, rsvp =>
            {
                rsvp.ToTable("GroupHikeRsvps");
                rsvp.WithOwner().HasForeignKey("HikeId");
                rsvp.HasKey("HikeId", nameof(HikeRsvp.UserId));
                rsvp.Property(r => r.UserId).HasMaxLength(128);
                rsvp.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            });
            hike.OwnsOne(h => h.MeetingPoint, p =>
            {
                p.Property(x => x.Latitude).HasColumnName("MeetingLatitude");
                p.Property(x => x.Longitude).HasColumnName("MeetingLongitude");
                p.Property(x => x.Elevation).HasColumnName("MeetingElevation");
            });
        });
    }
}
