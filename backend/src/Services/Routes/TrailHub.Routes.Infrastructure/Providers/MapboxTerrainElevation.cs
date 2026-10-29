using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>Adds ground elevation to a line.</summary>
internal interface IElevationSource
{
    /// <summary>The points with elevation, or null if it couldn't be looked up.</summary>
    Task<List<GeoPoint>?> TryAddElevationAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken);
}

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

        var tiles = new ConcurrentDictionary<(int X, int Y), Heightmap>();
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

    private async Task<Heightmap> LoadTileAsync(int x, int y, CancellationToken cancellationToken)
    {
        var key = $"terrain:{Zoom}:{x}:{y}";
        var png = await cache.GetAsync(key, cancellationToken);
        if (png is null)
        {
            png = await http.GetByteArrayAsync($"v4/mapbox.mapbox-terrain-dem-v1/{Zoom}/{x}/{y}@2x.pngraw?access_token={options.AccessToken}", cancellationToken);
            await cache.SetAsync(key, png, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheFor }, cancellationToken);
        }

        return Heightmap.FromTerrainPng(png);
    }

    /// <summary>A decoded tile: one height per pixel.</summary>
    internal sealed class Heightmap(int width, int height, float[] heights)
    {
        /// <summary>Bilinear interpolation between the four surrounding pixels.</summary>
        public double Sample(double x, double y)
        {
            x = Math.Clamp(x - 0.5, 0, width - 1);
            y = Math.Clamp(y - 0.5, 0, height - 1);
            var (x0, y0) = ((int)x, (int)y);
            var (x1, y1) = (Math.Min(x0 + 1, width - 1), Math.Min(y0 + 1, height - 1));
            var (tx, ty) = (x - x0, y - y0);
            double At(int px, int py) => heights[(py * width) + px];
            return (At(x0, y0) * (1 - tx) * (1 - ty)) + (At(x1, y0) * tx * (1 - ty)) + (At(x0, y1) * (1 - tx) * ty) + (At(x1, y1) * tx * ty);
        }

        /// <summary>
        /// Decodes an 8-bit RGB or RGBA, non-interlaced PNG (what Mapbox serves for <c>.pngraw</c>) and converts each
        /// pixel to a height. Small on purpose: a full image library isn't needed for one fixed format.
        /// </summary>
        public static Heightmap FromTerrainPng(byte[] png)
        {
            ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            if (png.Length < 33 || !png.AsSpan(0, 8).SequenceEqual(signature))
            {
                throw new InvalidDataException("Not a PNG.");
            }

            int width = 0, height = 0, channels = 0;
            using var idat = new MemoryStream();
            for (var offset = 8; offset + 8 <= png.Length;)
            {
                var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));
                var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
                var data = png.AsSpan(offset + 8, length);
                switch (type)
                {
                    case "IHDR":
                        width = (int)BinaryPrimitives.ReadUInt32BigEndian(data);
                        height = (int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
                        if (data[8] != 8 || data[12] != 0)
                        {
                            throw new InvalidDataException("Only 8-bit non-interlaced PNGs are supported.");
                        }

                        channels = data[9] switch
                        {
                            2 => 3,
                            6 => 4,
                            _ => throw new InvalidDataException("Only RGB and RGBA PNGs are supported."),
                        };
                        break;
                    case "IDAT":
                        idat.Write(data);
                        break;
                }

                offset += 12 + length;
            }

            var stride = width * channels;
            var raw = new byte[height * (stride + 1)];
            idat.Position = 0;
            using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            {
                z.ReadExactly(raw);
            }

            var pixels = new byte[height * stride];
            for (var row = 0; row < height; row++)
            {
                var filter = raw[row * (stride + 1)];
                var src = raw.AsSpan((row * (stride + 1)) + 1, stride);
                var dst = pixels.AsSpan(row * stride, stride);
                var prev = row > 0 ? pixels.AsSpan((row - 1) * stride, stride) : Span<byte>.Empty;
                for (var i = 0; i < stride; i++)
                {
                    int a = i >= channels ? dst[i - channels] : 0, b = row > 0 ? prev[i] : 0, c = i >= channels && row > 0 ? prev[i - channels] : 0;
                    dst[i] = (byte)(src[i] + filter switch
                    {
                        0 => 0,
                        1 => a,
                        2 => b,
                        3 => (a + b) / 2,
                        4 => Paeth(a, b, c),
                        _ => throw new InvalidDataException("Bad PNG filter."),
                    });
                }
            }

            var heights = new float[width * height];
            for (var p = 0; p < heights.Length; p++)
            {
                var i = p * channels;
                heights[p] = (float)(-10000 + (((pixels[i] * 65536) + (pixels[i + 1] * 256) + pixels[i + 2]) * 0.1));
            }

            return new Heightmap(width, height, heights);
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}

/// <summary>Mapbox terrain first; Open-Meteo when there's no Mapbox token or the tiles can't be fetched.</summary>
internal sealed class ElevationSource(MapboxTerrainElevation mapbox, OpenMeteoElevation openMeteo, MapboxOptions options) : IElevationSource
{
    public async Task<List<GeoPoint>?> TryAddElevationAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken) =>
        (options.Configured ? await mapbox.TryAddElevationAsync(points, cancellationToken) : null)
        ?? await openMeteo.TryAddElevationAsync(points, cancellationToken);
}
