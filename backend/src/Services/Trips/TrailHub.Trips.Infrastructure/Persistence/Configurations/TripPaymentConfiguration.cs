using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Infrastructure.Persistence.Configurations;

internal sealed class TripPaymentConfiguration : IEntityTypeConfiguration<TripPayment>
{
    public void Configure(EntityTypeBuilder<TripPayment> payment)
    {
        payment.ToTable("TripPayments");
        payment.HasKey(p => p.Id);
        payment.HasOne<Trip>().WithMany().HasForeignKey(p => p.TripId).OnDelete(DeleteBehavior.Cascade);
        payment.HasIndex(p => p.TripId);
        payment.Property(p => p.From).HasMaxLength(128).IsRequired();
        payment.Property(p => p.To).HasMaxLength(128).IsRequired();
        payment.Property(p => p.Amount).HasPrecision(12, 2);
        payment.Property(p => p.RecordedBy).HasMaxLength(128).IsRequired();
        payment.Ignore(p => p.KnownNames);
        payment.Property<Dictionary<string, string>>("_names").AsJsonMap("Names");
    }
}
