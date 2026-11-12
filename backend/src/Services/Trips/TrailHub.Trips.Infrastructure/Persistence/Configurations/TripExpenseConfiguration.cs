using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Infrastructure.Persistence.Configurations;

internal sealed class TripExpenseConfiguration : IEntityTypeConfiguration<TripExpense>
{
    public void Configure(EntityTypeBuilder<TripExpense> expense)
    {
        expense.ToTable("TripExpenses");
        expense.HasKey(e => e.Id);
        expense.HasOne<Trip>().WithMany().HasForeignKey(e => e.TripId).OnDelete(DeleteBehavior.Cascade);
        expense.HasIndex(e => e.TripId);
        expense.Property(e => e.Description).HasMaxLength(TripExpense.MaxDescriptionLength).IsRequired();
        expense.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
        expense.Property(e => e.PaidBy).HasMaxLength(128).IsRequired();
        expense.Property(e => e.Amount).HasPrecision(12, 2);
        expense.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        expense.Property(e => e.Rate).HasPrecision(18, Money.RateDecimals);
        expense.Property(e => e.AmountInTripCurrency).HasPrecision(12, 2);
        expense.Property(e => e.SplitMode).HasConversion<string>().HasMaxLength(10);
        expense.Property(e => e.CreatedBy).HasMaxLength(128).IsRequired();
        expense.Ignore(e => e.Shares);
        expense.Property<List<ExpenseShare>>("_shares").AsJsonList("Shares");
        expense.Ignore(e => e.KnownNames);
        expense.Property<Dictionary<string, string>>("_names").AsJsonMap("Names");

        // Several people may edit the same expense; the second save must not silently win.
        expense.Property<uint>("xmin").IsRowVersion();
    }
}
