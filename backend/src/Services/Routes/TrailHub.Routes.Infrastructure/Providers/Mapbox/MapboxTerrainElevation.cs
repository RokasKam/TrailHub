using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Routes.Infrastructure.Providers.Mapbox;

/// <summary>
/// Elevation from <see href="https://docs.mapbox.com/data/tilesets/reference/mapbox-terrain-dem-v1/">Mapbox Terrain-DEM</see>
/// tiles: the elevation data behind the app's terrain map, encoded in the colours of PNG tiles
/// (<c>height = -10000 + (R·65536 + G·256 + B) / 10</c> metres). At zoom 12 @2x a pixel is about 13 m in the Alps, and a
/// 150 km route touches a few dozen tiles, which are cached for a week.
/// </summary>
internal sealed class MapboxTerrainElevation(HttpClient http, MapboxOptions options, IDistributedCache cache, ILogger<MapboxTerrainElevation> logger) : IElevationSource
{
    private const int Zoom = 12;
    private const int TileSize = 512; // @2x
    private const int MaxTiles = 200;
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(7);

    public async Task<List<GeoPoint>?> TryAddElevationAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        var pixels = points.Select(p => GlobalPixel(p.Latitude, p.Longitude)).ToList();
        var tileKeys = pixels.Select(p => ((int)Math.Floor(p.X / TileSize), (int)Math.Floor(p.Y / TileSize))).Distinct().ToList();
        if (tileKeys.Count > MaxTiles)
        {
            return null;
        }

        var tiles = new ConcurrentDictionary<(int X, int Y), TerrainHeightmap>();
        try
        {
            await Parallel.ForEachAsync(tileKeys, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken }, async (key, ct) =>
                tiles[key] = await LoadTileAsync(key.Item1, key.Item2, ct));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Mapbox terrain tiles unavailable");
            return null;
        }

        return points.Select((p, i) =>
        {
            var (x, y) = pixels[i];
            var tile = tiles[((int)Math.Floor(x / TileSize), (int)Math.Floor(y / TileSize))];
            return new GeoPoint(p.Latitude, p.Longitude, Math.Round(tile.Sample(x - (Math.Floor(x / TileSize) * TileSize), y - (Math.Floor(y / TileSize) * TileSize)), 1));
        }).ToList();
    }

    /// <summary>Web Mercator pixel coordinates at <see cref="Zoom"/>.</summary>
    private static (double X, double Y) GlobalPixel(double latitude, double longitude)
    {
        var scale = TileSize * Math.Pow(2, Zoom);
        var sin = Math.Sin(latitude * Math.PI / 180);
        return ((longitude + 180) / 360 * scale, (0.5 - (Math.Log((1 + sin) / (1 - sin)) / (4 * Math.PI))) * scale);
    }

    private async Task<TerrainHeightmap> LoadTileAsync(int x, int y, CancellationToken cancellationToken)
    {
        var key = $"terrain:{Zoom}:{x}:{y}";
        var png = await cache.GetAsync(key, cancellationToken);
        if (png is null)
        {
            png = await http.GetByteArrayAsync($"v4/mapbox.mapbox-terrain-dem-v1/{Zoom}/{x}/{y}@2x.pngraw?access_token={options.AccessToken}", cancellationToken);
            await cache.SetAsync(key, png, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheFor }, cancellationToken);
        }

        return TerrainHeightmap.FromTerrainPng(png);
    }
}
