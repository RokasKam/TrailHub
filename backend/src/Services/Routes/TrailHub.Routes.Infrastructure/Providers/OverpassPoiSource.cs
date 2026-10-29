using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Routes;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>
/// OpenStreetMap points of interest from the <see href="https://wiki.openstreetmap.org/wiki/Overpass_API">Overpass API</see>,
/// in a corridor around the line. Overpass's own <c>around</c> filter is slow on long lines (tens of seconds), so this
/// asks for a chain of small bounding boxes covering the line and keeps what's really within the corridor itself.
/// Answers are cached for a day: map data changes slowly and the public Overpass servers are shared and rate-limited.
/// </summary>
internal sealed class OverpassPoiSource(HttpClient http, OverpassOptions options, IDistributedCache cache, ILogger<OverpassPoiSource> logger) : IPoiSource
{
    private const int MaxLinePoints = 250;
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The server that answered last; tried first next time, so one busy server doesn't slow every lookup.</summary>
    private static Uri? _lastGood;

    // OSM tags per kind. Shelters only count when they're huts or lean-tos, not bus shelters.
    private static readonly Dictionary<PoiKind, string[]> Filters = new()
    {
        [PoiKind.WaterSource] = ["[\"amenity\"=\"drinking_water\"]", "[\"natural\"=\"spring\"]", "[\"amenity\"=\"water_point\"]"],
        [PoiKind.Campsite] = ["[\"tourism\"=\"camp_site\"]"],
        [PoiKind.MountainHut] = ["[\"tourism\"=\"alpine_hut\"]"],
        [PoiKind.Shelter] = ["[\"tourism\"=\"wilderness_hut\"]", "[\"amenity\"=\"shelter\"][\"shelter_type\"~\"^(basic_hut|lean_to)$\"]"],
        [PoiKind.Hotel] = ["[\"tourism\"~\"^(hotel|guest_house|hostel|chalet|apartment)$\"]"],
        [PoiKind.Viewpoint] = ["[\"tourism\"=\"viewpoint\"]"],
    };

    public async Task<Result<IReadOnlyList<Poi>>> AlongAsync(IReadOnlyList<GeoPoint> line, double corridorMeters, IReadOnlyCollection<PoiKind> kinds, CancellationToken cancellationToken)
    {
        var thinned = Thin(line, corridorMeters);
        var query = BuildQuery(Boxes(thinned, corridorMeters), kinds);
        var key = "overpass:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(query)))[..32];
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached && JsonSerializer.Deserialize<List<Poi>>(cached, Json) is { } hit)
        {
            return hit;
        }

        // The public servers are volunteer-run and often busy; try each until one answers.
        OverpassResponse? response = null;
        var servers = _lastGood is { } preferred ? options.Servers.Where(u => u == preferred).Concat(options.Servers.Where(u => u != preferred)) : options.Servers;
        foreach (var server in servers)
        {
            try
            {
                using var content = new FormUrlEncodedContent([new("data", query)]);
                using var reply = await http.PostAsync(new Uri(server, "api/interpreter"), content, cancellationToken);
                if (reply.IsSuccessStatusCode)
                {
                    response = await reply.Content.ReadFromJsonAsync<OverpassResponse>(Json, cancellationToken);
                    _lastGood = server;
                    break;
                }

                logger.LogWarning("Overpass server {Server} returned {Status}", server.Host, reply.StatusCode);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or TimeoutException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Overpass server {Server} failed: {Message}", server.Host, e.Message);
            }
        }

        if (response is null)
        {
            return RouteErrors.MapDataUnavailable;
        }

        var pois = (response?.Elements ?? [])
            .Select(ToPoi)
            .OfType<Poi>()
            .Where(p => kinds.Contains(p.Kind) && DistanceToLine(new GeoPoint(p.Latitude, p.Longitude), thinned) <= corridorMeters)
            .DistinctBy(p => p.Id)
            .ToList();
        await cache.SetStringAsync(key, JsonSerializer.Serialize(pois, Json), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheFor }, cancellationToken);
        return pois;
    }

    /// <summary>Keeps points about half a corridor apart (and at most <see cref="MaxLinePoints"/>) so the query stays small.</summary>
    internal static List<GeoPoint> Thin(IReadOnlyList<GeoPoint> line, double corridorMeters)
    {
        var total = line.Zip(line.Skip(1), RouteMetrics.HaversineMeters).Sum();
        var spacing = Math.Max(Math.Max(100, corridorMeters / 2), total / MaxLinePoints);
        var kept = new List<GeoPoint> { line[0] };
        double run = 0;
        for (var i = 1; i < line.Count; i++)
        {
            run += RouteMetrics.HaversineMeters(line[i - 1], line[i]);
            if (run >= spacing || i == line.Count - 1)
            {
                kept.Add(line[i]);
                run = 0;
            }
        }

        return kept;
    }

    /// <summary>Bounding boxes (south, west, north, east) along the line, each covering about 5 km of it plus the corridor.</summary>
    internal static List<(double S, double W, double N, double E)> Boxes(IReadOnlyList<GeoPoint> line, double corridorMeters)
    {
        const double ChunkMeters = 5000;
        var boxes = new List<(double, double, double, double)>();
        var start = 0;
        double run = 0;
        for (var i = 1; i < line.Count; i++)
        {
            run += RouteMetrics.HaversineMeters(line[i - 1], line[i]);
            if (run >= ChunkMeters || i == line.Count - 1)
            {
                var chunk = line.Skip(start).Take(i - start + 1).ToList();
                var padLat = corridorMeters / 111_320;
                var padLon = corridorMeters / (111_320 * Math.Cos(chunk[0].Latitude * Math.PI / 180));
                boxes.Add((chunk.Min(p => p.Latitude) - padLat, chunk.Min(p => p.Longitude) - padLon, chunk.Max(p => p.Latitude) + padLat, chunk.Max(p => p.Longitude) + padLon));
                start = i;
                run = 0;
            }
        }

        return boxes;
    }

    private static string BuildQuery(IReadOnlyList<(double S, double W, double N, double E)> boxes, IReadOnlyCollection<PoiKind> kinds)
    {
        var parts = kinds.Distinct()
            .SelectMany(k => Filters[k])
            .SelectMany(f => boxes.Select(b => string.Create(CultureInfo.InvariantCulture, $"nwr{f}({b.S:F5},{b.W:F5},{b.N:F5},{b.E:F5});")));
        return $"[out:json][timeout:25];({string.Concat(parts)});out center tags;";
    }

    /// <summary>Shortest distance from a point to a polyline, in meters (flat-earth approximation, fine at corridor scale).</summary>
    internal static double DistanceToLine(GeoPoint p, IReadOnlyList<GeoPoint> line)
    {
        var cos = Math.Cos(p.Latitude * Math.PI / 180);
        (double X, double Y) Local(GeoPoint q) => ((q.Longitude - p.Longitude) * 111_320 * cos, (q.Latitude - p.Latitude) * 111_320);

        var best = double.MaxValue;
        for (var i = 0; i < line.Count; i++)
        {
            var a = Local(line[i]);
            if (i == 0 || line.Count == 1)
            {
                best = Math.Min(best, Math.Sqrt((a.X * a.X) + (a.Y * a.Y)));
                continue;
            }

            var b = Local(line[i - 1]);
            var (dx, dy) = (a.X - b.X, a.Y - b.Y);
            var lengthSquared = (dx * dx) + (dy * dy);
            var t = lengthSquared == 0 ? 0 : Math.Clamp(-((b.X * dx) + (b.Y * dy)) / lengthSquared, 0, 1);
            var (cx, cy) = (b.X + (t * dx), b.Y + (t * dy));
            best = Math.Min(best, Math.Sqrt((cx * cx) + (cy * cy)));
        }

        return best;
    }

    private static Poi? ToPoi(Element e)
    {
        var lat = e.Lat ?? e.Center?.Lat;
        var lon = e.Lon ?? e.Center?.Lon;
        if (lat is null || lon is null || e.Tags is null)
        {
            return null;
        }

        string? Tag(string name) => e.Tags.TryGetValue(name, out var v) ? v : null;
        PoiKind? kind = (Tag("tourism"), Tag("amenity"), Tag("natural")) switch
        {
            (_, "drinking_water" or "water_point", _) or (_, _, "spring") => PoiKind.WaterSource,
            ("camp_site", _, _) => PoiKind.Campsite,
            ("alpine_hut", _, _) => PoiKind.MountainHut,
            ("wilderness_hut", _, _) or (_, "shelter", _) => PoiKind.Shelter,
            ("hotel" or "guest_house" or "hostel" or "chalet" or "apartment", _, _) => PoiKind.Hotel,
            ("viewpoint", _, _) => PoiKind.Viewpoint,
            _ => null,
        };
        if (kind is null)
        {
            return null;
        }

        var name = Tag("name") ?? kind switch
        {
            PoiKind.WaterSource => Tag("natural") == "spring" ? "Spring" : "Drinking water",
            PoiKind.Campsite => "Campsite",
            PoiKind.MountainHut => "Mountain hut",
            PoiKind.Shelter => "Shelter",
            PoiKind.Hotel => "Accommodation",
            _ => "Viewpoint",
        };
        double? elevation = double.TryParse(Tag("ele")?.Replace(" m", "", StringComparison.Ordinal), NumberStyles.Float, CultureInfo.InvariantCulture, out var ele) ? ele : null;
        return new Poi($"{e.Type}/{e.Id}", name, kind.Value, lat.Value, lon.Value, elevation);
    }

    private sealed record OverpassResponse(IReadOnlyList<Element>? Elements);

    private sealed record Element(string Type, long Id, double? Lat, double? Lon, Center? Center, Dictionary<string, string>? Tags);

    private sealed record Center(double Lat, double Lon);
}

/// <summary>Overpass servers to try in order (<c>Routing:Overpass:Servers</c>).</summary>
internal sealed record OverpassOptions(IReadOnlyList<Uri> Servers);
