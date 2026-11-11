using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Infrastructure.Persistence.Configurations;

internal sealed class GearListConfiguration : IEntityTypeConfiguration<GearList>
{
    private static readonly JsonSerializerOptions GearJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Configure(EntityTypeBuilder<GearList> gear)
    {
        gear.HasKey(g => g.Id);
        gear.Property(g => g.OwnerId).HasMaxLength(128).IsRequired();
        gear.Property(g => g.Name).HasMaxLength(GearList.MaxNameLength).IsRequired();
        gear.HasIndex(g => g.OwnerId);
        gear.Ignore(g => g.Items);
        gear.Property<List<GearItem>>("_items")
            .HasColumnName("Items")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, GearJson),
                v => JsonSerializer.Deserialize<List<GearItem>>(v, GearJson) ?? new List<GearItem>(),
                new ValueComparer<List<GearItem>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (h, i) => HashCode.Combine(h, i.GetHashCode())),
                    v => v.ToList()));
    }
}
