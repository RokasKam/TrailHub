using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>
/// Ground elevation for a line from Open-Meteo's 90 m Copernicus DEM (free, no key). A routed line can have thousands
/// of points, so it samples about every 100 m (at most <see cref="MaxSamples"/> points, a handful of requests) and
/// interpolates between samples by distance. That's finer than the DEM itself, so nothing is lost.
/// </summary>
internal sealed class OpenMeteoElevation(HttpClient http, ILogger<OpenMeteoElevation> logger) : IElevationSource
{
    private const int MaxSamples = 400;
    private const int Batch = 100; // Open-Meteo's limit per request
    private const double SpacingMeters = 100;

    public async Task<List<GeoPoint>?> TryAddElevationAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        var distance = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
        {
            distance[i] = distance[i - 1] + RouteMetrics.HaversineMeters(points[i - 1], points[i]);
        }

        // Sample indices along the line, always including both ends.
        var spacing = Math.Max(SpacingMeters, distance[^1] / (MaxSamples - 1));
        var samples = new List<int> { 0 };
        for (var i = 1; i < points.Count - 1; i++)
        {
            if (distance[i] - distance[samples[^1]] >= spacing)
            {
                samples.Add(i);
            }
        }

        samples.Add(points.Count - 1);

        var heights = await LookupAsync(samples.Select(i => points[i]).ToList(), cancellationToken);
        if (heights is null)
        {
            return null;
        }

        var result = new List<GeoPoint>(points.Count);
        var s = 0;
        for (var i = 0; i < points.Count; i++)
        {
            while (s < samples.Count - 2 && samples[s + 1] < i)
            {
                s++;
            }

            var (a, b) = (samples[s], samples[Math.Min(s + 1, samples.Count - 1)]);
            var t = distance[b] > distance[a] ? Math.Clamp((distance[i] - distance[a]) / (distance[b] - distance[a]), 0, 1) : 0;
            double? elevation = heights[s] is { } ha && heights[Math.Min(s + 1, heights.Length - 1)] is { } hb ? ha + ((hb - ha) * t) : heights[s];
            result.Add(new GeoPoint(points[i].Latitude, points[i].Longitude, elevation is { } e ? Math.Round(e, 1) : null));
        }

        return result;
    }

    private async Task<double?[]?> LookupAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        try
        {
            var result = new double?[points.Count];
            for (var start = 0; start < points.Count; start += Batch)
            {
                var batch = points.Skip(start).Take(Batch).ToList();
                var lat = string.Join(',', batch.Select(p => p.Latitude.ToString("F5", CultureInfo.InvariantCulture)));
                var lon = string.Join(',', batch.Select(p => p.Longitude.ToString("F5", CultureInfo.InvariantCulture)));
                var response = await http.GetFromJsonAsync<ElevationResponse>($"v1/elevation?latitude={lat}&longitude={lon}", cancellationToken);
                for (var i = 0; i < batch.Count && i < (response?.Elevation.Count ?? 0); i++)
                {
                    result[start + i] = response!.Elevation[i];
                }
            }

            return result;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Open-Meteo elevation lookup failed; the route has no elevation");
            return null;
        }
    }

    private sealed record ElevationResponse(IReadOnlyList<double?> Elevation);
}
