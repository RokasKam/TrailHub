using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Providers.Mapbox;

/// <summary>
/// <see href="https://docs.mapbox.com/api/navigation/directions/">Mapbox Directions</see> with the walking profile,
/// biased towards footpaths (<c>walkway_bias=1</c>), so routes follow trails on the same map the app shows.
/// Mapbox Directions doesn't return elevation or trail grades: elevation comes from <see cref="IElevationSource"/>, and the
/// grade is unknown (SAC 0). Requests take at most 25 waypoints, so longer routes are routed in chunks.
/// </summary>
internal sealed class MapboxDirectionsRouter(HttpClient http, MapboxOptions options, IElevationSource elevation, ILogger<MapboxDirectionsRouter> logger) : IRouter
{
    private const int MaxWaypointsPerRequest = 25;

    /// <summary>A waypoint further than this from any path is reported as unreachable rather than silently moved.</summary>
    private const double MaxSnapMeters = 3000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<RoutedTrack>> RouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        // Chunks share their end/start waypoint: [0..24], [24..48], …
        var points = new List<GeoPoint>();
        for (var start = 0; start < waypoints.Count - 1; start += MaxWaypointsPerRequest - 1)
        {
            var chunk = waypoints.Skip(start).Take(MaxWaypointsPerRequest).ToList();
            var routes = await RequestAsync(chunk, alternatives: false, cancellationToken);
            if (routes.IsFailure)
            {
                return routes.Error!;
            }

            points.AddRange(points.Count == 0 ? routes.Value[0] : routes.Value[0].Skip(1));
        }

        return await ToRoutedTrackAsync(points, waypoints, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<RoutedTrack>>> AlternativesAsync(GeoPoint from, GeoPoint to, int count, CancellationToken cancellationToken)
    {
        var routes = await RequestAsync([from, to], alternatives: count > 1, cancellationToken);
        if (routes.IsFailure)
        {
            return routes.Error!;
        }

        var tracks = new List<RoutedTrack>();
        foreach (var line in routes.Value.Take(count))
        {
            tracks.Add(await ToRoutedTrackAsync(line, [from, to], cancellationToken));
        }

        return tracks;
    }

    private async Task<RoutedTrack> ToRoutedTrackAsync(List<GeoPoint> line, IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        var withElevation = await elevation.TryAddElevationAsync(line, cancellationToken) ?? line;
        return new RoutedTrack(new Track(withElevation), LocateWaypoints(withElevation, waypoints), Snapped: true);
    }

    /// <summary>Where each waypoint lies on the line: the nearest point, searching forward so loops and out-and-backs resolve in order.</summary>
    internal static List<int> LocateWaypoints(IReadOnlyList<GeoPoint> line, IReadOnlyList<GeoPoint> waypoints)
    {
        var indices = new List<int> { 0 };
        var from = 0;
        for (var w = 1; w < waypoints.Count - 1; w++)
        {
            var best = from;
            var bestDistance = double.MaxValue;
            for (var i = from; i < line.Count; i++)
            {
                var d = RouteMetrics.HaversineMeters(line[i], waypoints[w]);
                if (d < bestDistance)
                {
                    (best, bestDistance) = (i, d);
                }
            }

            indices.Add(best);
            from = best;
        }

        indices.Add(line.Count - 1);
        return indices;
    }

    /// <summary>One request: the main route's line, plus alternatives' lines when asked.</summary>
    private async Task<Result<List<List<GeoPoint>>>> RequestAsync(IReadOnlyList<GeoPoint> waypoints, bool alternatives, CancellationToken cancellationToken)
    {
        var coordinates = string.Join(';', waypoints.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Longitude:F6},{p.Latitude:F6}")));
        var url = $"directions/v5/mapbox/walking/{coordinates}?geometries=geojson&overview=full&walkway_bias=1&alternatives={(alternatives ? "true" : "false")}&access_token={options.AccessToken}";

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Mapbox Directions request failed");
            return RouteErrors.RouterUnavailable;
        }

        using (response)
        {
            MapboxDirectionsResponse? body = null;
            try
            {
                body = await response.Content.ReadFromJsonAsync<MapboxDirectionsResponse>(Json, cancellationToken);
            }
            catch (JsonException)
            {
            }

            // Mapbox reports routing problems in "code" (often with HTTP 422), not only via the status.
            switch (body?.Code)
            {
                case "Ok" when body.Routes is { Count: > 0 }:
                    if (body.Waypoints?.Any(w => w.Distance > MaxSnapMeters) == true)
                    {
                        return RouteErrors.NoPath;
                    }

                    return body.Routes.Select(r => r.Geometry.Coordinates.Select(c => new GeoPoint(c[1], c[0])).ToList()).ToList();
                case "NoRoute" or "NoSegment" or "Ok":
                    return RouteErrors.NoPath;
                case "InvalidInput" when body.Message?.Contains("distance", StringComparison.OrdinalIgnoreCase) == true:
                    return RouteErrors.TooLong;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogError("Mapbox rejected the access token ({Status}). Check Routing:Mapbox:AccessToken.", response.StatusCode);
            }
            else
            {
                logger.LogWarning("Mapbox Directions returned {Status} {Code}: {Message}", response.StatusCode, body?.Code, body?.Message);
            }

            return RouteErrors.RouterUnavailable;
        }
    }
}
