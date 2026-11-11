using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Domain.Routes;
using TrailHub.Routes.Infrastructure.Providers.Overpass;

namespace TrailHub.Routes.Infrastructure.Providers.Mapbox;

/// <summary>
/// Points of interest from the map tiles the app already shows, via the
/// <see href="https://docs.mapbox.com/api/maps/tilequery/">Mapbox Tilequery API</see>: circles placed along the line,
/// each returning nearby POI labels. Fast and reliable, so it's the main source; Overpass has more springs and small
/// shelters but its public servers are often busy. Needs <c>Routing:Mapbox:AccessToken</c>.
/// </summary>
internal sealed class MapboxPoiSource(HttpClient http, MapboxOptions options, ILogger<MapboxPoiSource> logger) : IPoiSource
{
    private const int MaxQueries = 120;
    private const int Parallelism = 8;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, PoiKind> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alpine Hut"] = PoiKind.MountainHut,
        ["Wilderness Hut"] = PoiKind.Shelter,
        ["Shelter"] = PoiKind.Shelter,
        ["Camp Site"] = PoiKind.Campsite,
        ["Caravan Site"] = PoiKind.Campsite,
        ["Hotel"] = PoiKind.Hotel,
        ["Guest House"] = PoiKind.Hotel,
        ["Hostel"] = PoiKind.Hotel,
        ["Chalet"] = PoiKind.Hotel,
        ["Motel"] = PoiKind.Hotel,
        ["Viewpoint"] = PoiKind.Viewpoint,
        ["Drinking Water"] = PoiKind.WaterSource,
        ["Spring"] = PoiKind.WaterSource,
        ["Water Point"] = PoiKind.WaterSource,
    };

    public async Task<Result<IReadOnlyList<Poi>>> AlongAsync(IReadOnlyList<GeoPoint> line, double corridorMeters, IReadOnlyCollection<PoiKind> kinds, CancellationToken cancellationToken)
    {
        // Overlapping circles of the corridor's radius, centred every 1.6 radii along the line.
        var radius = Math.Max(corridorMeters, 300);
        var total = line.Zip(line.Skip(1), RouteMetrics.HaversineMeters).Sum();
        var centers = Sample(line, Math.Max(radius * 1.6, total / MaxQueries));

        var found = new List<Poi>();
        var failures = 0;
        await Parallel.ForEachAsync(centers, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken }, async (center, ct) =>
        {
            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"v4/mapbox.mapbox-streets-v8/tilequery/{center.Longitude:F5},{center.Latitude:F5}.json?radius={Math.Round(radius)}&limit=50&layers=poi_label&dedupe=true&access_token={options.AccessToken}");
            try
            {
                var response = await http.GetFromJsonAsync<MapboxTilequeryFeatureCollection>(url, Json, ct);
                var pois = (response?.Features ?? []).Select(ToPoi).OfType<Poi>().Where(p => kinds.Contains(p.Kind)).ToList();
                lock (found)
                {
                    found.AddRange(pois);
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref failures);
                logger.LogWarning("Mapbox Tilequery failed: {Message}", e.Message);
            }
        });

        if (failures == centers.Count)
        {
            return RouteErrors.MapDataUnavailable;
        }

        return found
            .DistinctBy(p => p.Id)
            .Where(p => OverpassPoiSource.DistanceToLine(new GeoPoint(p.Latitude, p.Longitude), line) <= corridorMeters)
            .ToList();
    }

    private static List<GeoPoint> Sample(IReadOnlyList<GeoPoint> line, double spacing)
    {
        var points = new List<GeoPoint> { line[0] };
        double run = 0;
        for (var i = 1; i < line.Count; i++)
        {
            run += RouteMetrics.HaversineMeters(line[i - 1], line[i]);
            if (run >= spacing)
            {
                points.Add(line[i]);
                run = 0;
            }
        }

        if (points[^1] != line[^1])
        {
            points.Add(line[^1]);
        }

        return points;
    }

    private static Poi? ToPoi(MapboxTilequeryFeature f)
    {
        if (f.Geometry.Coordinates.Length < 2 || f.Properties.Type is null || !Types.TryGetValue(f.Properties.Type, out var kind))
        {
            return null;
        }

        var name = f.Properties.Name ?? f.Properties.Type;
        return new Poi($"mapbox/{f.Id}", name, kind, f.Geometry.Coordinates[1], f.Geometry.Coordinates[0], null);
    }
}
