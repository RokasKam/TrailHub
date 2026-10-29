using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Routes;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>
/// <see href="https://openrouteservice.org/dev/#/api-docs/v2/directions">OpenRouteService</see> directions with the
/// <c>foot-hiking</c> profile, which prefers footpaths and marked hiking routes from OpenStreetMap. Returns elevation
/// per point and the SAC trail grade (<c>traildifficulty</c>) per stretch. Needs <c>Routing:OpenRouteService:ApiKey</c>.
/// </summary>
internal sealed class OpenRouteServiceRouter(HttpClient http, RoutingOptions options, ILogger<OpenRouteServiceRouter> logger) : IRouter
{
    /// <summary>How far a waypoint may be from the nearest path and still snap to it.</summary>
    private const int SnapRadiusMeters = 2000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task<Result<RoutedTrack>> RouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        var result = await RequestAsync(waypoints, alternatives: null, cancellationToken);
        return result.IsSuccess ? result.Value[0] : result.Error!;
    }

    public async Task<Result<IReadOnlyList<RoutedTrack>>> AlternativesAsync(GeoPoint from, GeoPoint to, int count, CancellationToken cancellationToken)
    {
        var result = await RequestAsync([from, to], new AlternativeRoutes(count, WeightFactor: 1.6, ShareFactor: 0.6), cancellationToken);

        // ORS only computes alternatives for shorter routes; past that limit, offer the one best route.
        return result.Error?.Code == RouteErrors.TooLong.Code
            ? await RequestAsync([from, to], alternatives: null, cancellationToken)
            : result;
    }

    private async Task<Result<IReadOnlyList<RoutedTrack>>> RequestAsync(IReadOnlyList<GeoPoint> waypoints, AlternativeRoutes? alternatives, CancellationToken cancellationToken)
    {
        var body = new DirectionsRequest(
            waypoints.Select(p => new[] { p.Longitude, p.Latitude }).ToList(),
            Elevation: true,
            Instructions: false,
            ExtraInfo: ["traildifficulty"],
            Radiuses: waypoints.Select(_ => SnapRadiusMeters).ToList(),
            AlternativeRoutes: alternatives);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/directions/foot-hiking/geojson") { Content = JsonContent.Create(body, options: Json) };
        request.Headers.TryAddWithoutValidation("Authorization", options.OpenRouteServiceApiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "OpenRouteService request failed");
            return RouteErrors.RouterUnavailable;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return await ErrorFromAsync(response, cancellationToken);
            }

            var collection = await response.Content.ReadFromJsonAsync<FeatureCollection>(Json, cancellationToken);
            var tracks = (collection?.Features ?? []).Select(ToRoutedTrack).Where(t => t is not null).Select(t => t!).ToList();
            return tracks.Count > 0 ? tracks : RouteErrors.NoPath;
        }
    }

    private static RoutedTrack? ToRoutedTrack(Feature feature)
    {
        var coordinates = feature.Geometry.Coordinates;
        if (coordinates.Count < 2)
        {
            return null;
        }

        var points = coordinates.Select(c => new GeoPoint(c[1], c[0], c.Length > 2 ? c[2] : null)).ToList();

        // traildifficulty comes as [fromIndex, toIndex, grade] runs; spread it onto the points.
        var grades = new byte[points.Count];
        foreach (var run in feature.Properties.Extras?.TrailDifficulty?.Values ?? [])
        {
            for (var i = (int)run[0]; i <= Math.Min((int)run[1], grades.Length - 1); i++)
            {
                grades[i] = (byte)Math.Clamp(run[2], 0, 6);
            }
        }

        return new RoutedTrack(new Track(points, grades), feature.Properties.WayPoints, Snapped: true);
    }

    private async Task<Error> ErrorFromAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        int? code = null;
        try
        {
            code = JsonSerializer.Deserialize<ErrorBody>(text, Json)?.Error?.Code;
        }
        catch (JsonException)
        {
        }

        // 2004: request exceeds limits (distance, alternatives); 2009: no route; 2010: a point isn't near any path.
        switch (code)
        {
            case 2004:
                return RouteErrors.TooLong;
            case 2009 or 2010:
                return RouteErrors.NoPath;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogError("OpenRouteService rejected the API key ({Status}). Check Routing:OpenRouteService:ApiKey.", response.StatusCode);
        }
        else
        {
            logger.LogWarning("OpenRouteService returned {Status}: {Body}", response.StatusCode, text.Length > 500 ? text[..500] : text);
        }

        return RouteErrors.RouterUnavailable;
    }

    private sealed record DirectionsRequest(
        IReadOnlyList<double[]> Coordinates,
        bool Elevation,
        bool Instructions,
        [property: JsonPropertyName("extra_info")] IReadOnlyList<string> ExtraInfo,
        IReadOnlyList<int> Radiuses,
        [property: JsonPropertyName("alternative_routes")] AlternativeRoutes? AlternativeRoutes);

    private sealed record AlternativeRoutes(
        [property: JsonPropertyName("target_count")] int TargetCount,
        [property: JsonPropertyName("weight_factor")] double WeightFactor,
        [property: JsonPropertyName("share_factor")] double ShareFactor);

    private sealed record FeatureCollection(IReadOnlyList<Feature>? Features);

    private sealed record Feature(LineString Geometry, FeatureProperties Properties);

    private sealed record LineString(IReadOnlyList<double[]> Coordinates);

    private sealed record FeatureProperties([property: JsonPropertyName("way_points")] IReadOnlyList<int> WayPoints, Extras? Extras);

    private sealed record Extras([property: JsonPropertyName("traildifficulty")] ExtraValues? TrailDifficulty);

    private sealed record ExtraValues(IReadOnlyList<int[]> Values);

    private sealed record ErrorBody(ErrorDetail? Error);

    private sealed record ErrorDetail(int? Code, string? Message);
}
