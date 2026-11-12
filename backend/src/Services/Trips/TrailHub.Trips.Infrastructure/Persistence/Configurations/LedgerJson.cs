using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrailHub.Trips.Infrastructure.Persistence.Configurations;

/// <summary>jsonb columns for the expense ledger: shares and remembered display names.</summary>
internal static class LedgerJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static PropertyBuilder<List<T>> AsJsonList<T>(this PropertyBuilder<List<T>> property, string column) =>
        property
            .HasColumnName(column)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Options),
                v => JsonSerializer.Deserialize<List<T>>(v, Options) ?? new List<T>(),
                new ValueComparer<List<T>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (h, i) => HashCode.Combine(h, i!.GetHashCode())),
                    v => v.ToList()));

    public static PropertyBuilder<Dictionary<string, string>> AsJsonMap(this PropertyBuilder<Dictionary<string, string>> property, string column) =>
        property
            .HasColumnName(column)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Options),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, Options) ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                    v => v.OrderBy(p => p.Key, StringComparer.Ordinal).Aggregate(0, (h, p) => HashCode.Combine(h, p.Key.GetHashCode(StringComparison.Ordinal), p.Value.GetHashCode(StringComparison.Ordinal))),
                    v => new Dictionary<string, string>(v)));
}
