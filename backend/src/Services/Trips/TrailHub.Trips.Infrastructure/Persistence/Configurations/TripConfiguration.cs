using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Infrastructure.Persistence.Configurations;

internal sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> trip)
    {
        trip.HasKey(t => t.Id);
        trip.Property(t => t.OwnerId).HasMaxLength(128).IsRequired();
        trip.Property(t => t.Name).HasMaxLength(Trip.MaxNameLength).IsRequired();
        trip.Property(t => t.Description).HasMaxLength(Trip.MaxTextLength);
        trip.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        trip.HasIndex(t => new { t.OwnerId, t.StartDate });
        trip.HasIndex(t => t.GroupId);
        trip.Property(t => t.Currency).HasMaxLength(3).IsRequired().HasDefaultValue(CurrencyCode.Default);
        trip.Ignore(t => t.DayCount);
        trip.Property<uint>("xmin").IsRowVersion();

        trip.OwnsMany(t => t.Days, day =>
        {
            day.ToTable("TripDays");
            day.WithOwner().HasForeignKey("TripId");
            day.HasKey("TripId", nameof(TripDay.DayNumber));
            day.Property(d => d.Notes).HasMaxLength(Trip.MaxTextLength);
            day.Property(d => d.Overnight).HasMaxLength(Trip.MaxNameLength);
            day.Property(d => d.TrailIds); // uuid[] column
        });

        trip.OwnsMany(t => t.Checklist, item =>
        {
            item.ToTable("ChecklistItems");
            item.WithOwner().HasForeignKey("TripId");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();
            item.Property(i => i.Text).HasMaxLength(Trip.MaxNameLength).IsRequired();
            item.Property(i => i.Kind).HasConversion<string>().HasMaxLength(20);
            item.Property(i => i.AddedBy).HasMaxLength(128).IsRequired();
            item.Property(i => i.ClaimedBy).HasMaxLength(128);
            item.Property(i => i.DoneBy); // text[] column

            // Items are edited by several people; two claims on the same item must not both win.
            item.Property<uint>("xmin").IsRowVersion();
        });
    }
}
