using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Persistence.Configurations;

internal sealed class PlannedRouteConfiguration : IEntityTypeConfiguration<PlannedRoute>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<PlannedRoute> route)
    {
        route.HasKey(r => r.Id);
        route.Property(r => r.OwnerId).HasMaxLength(128).IsRequired();
        route.Property(r => r.Name).HasMaxLength(PlannedRoute.MaxNameLength).IsRequired();
        route.Property(r => r.Description).HasMaxLength(PlannedRoute.MaxTextLength);
        route.HasIndex(r => new { r.OwnerId, r.UpdatedAtUtc });
        route.Ignore(r => r.DayCount);
        route.Property<uint>("xmin").IsRowVersion();

        // Waypoints and the routed line are always read and written whole, so they live in jsonb columns.
        route.Ignore(r => r.Waypoints);
        route.Property<List<RouteWaypoint>>("_waypoints")
            .HasColumnName("Waypoints")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<RouteWaypoint>>(v, Json) ?? new List<RouteWaypoint>(),
                JsonComparer<List<RouteWaypoint>>());

        // [lat, lon, elevation] triples: about a third of the size of named fields for a long route.
        route.Ignore(r => r.Points);
        route.Property<List<GeoPoint>>("_points")
            .HasColumnName("Points")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Select(p => new[] { p.Latitude, p.Longitude, p.Elevation }), Json),
                v => (JsonSerializer.Deserialize<double?[][]>(v, Json) ?? Array.Empty<double?[]>()).Select(p => new GeoPoint(p[0]!.Value, p[1]!.Value, p[2])).ToList(),
                new ValueComparer<List<GeoPoint>>((a, b) => ReferenceEquals(a, b), v => v.Count, v => v));

        route.Property(r => r.SacScale);
        route.Property(r => r.WaypointIndices);
        route.Property(r => r.SharedWithGroups); // uuid[]
        route.HasIndex(r => r.SharedWithGroups).HasMethod("gin"); // "shared with any of my groups" lookups
    }

    private static ValueComparer<T> JsonComparer<T>()
        where T : class => new(
            (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
            v => JsonSerializer.Serialize(v, Json).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Json), Json)!);
}
