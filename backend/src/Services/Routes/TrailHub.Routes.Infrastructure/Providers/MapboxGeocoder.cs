using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Routes;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>
/// Place search with the <see href="https://docs.mapbox.com/api/search/search-box/">Mapbox Search Box API</see>:
/// towns and also points of interest such as huts, so "Cabane des Vignettes" finds the hut. Results near
/// <c>near</c> rank higher.
/// </summary>
internal sealed class MapboxGeocoder(HttpClient http, MapboxOptions options, ILogger<MapboxGeocoder> logger) : IGeocoder
{
    public async Task<Result<IReadOnlyList<Place>>> SearchAsync(string text, GeoPoint? near, CancellationToken cancellationToken)
    {
        var url = $"search/searchbox/v1/forward?q={Uri.EscapeDataString(text)}&limit=8&language=en&access_token={options.AccessToken}";
        if (near is not null)
        {
            url += string.Create(CultureInfo.InvariantCulture, $"&proximity={near.Longitude:F4},{near.Latitude:F4}");
        }

        try
        {
            var response = await http.GetFromJsonAsync<FeatureCollection>(url, cancellationToken);
            return (response?.Features ?? [])
                .Where(f => f.Properties.Name is not null && f.Geometry.Coordinates.Length >= 2)
                .Select(f => new Place(f.Properties.Name!, f.Properties.PlaceFormatted, f.Geometry.Coordinates[1], f.Geometry.Coordinates[0]))
                .DistinctBy(p => (p.Name, p.Region))
                .ToList();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Mapbox search failed");
            return RouteErrors.RouterUnavailable;
        }
    }

    private sealed record FeatureCollection(IReadOnlyList<Feature>? Features);

    private sealed record Feature(Geometry Geometry, Properties Properties);

    private sealed record Geometry(double[] Coordinates);

    private sealed record Properties(string? Name, [property: System.Text.Json.Serialization.JsonPropertyName("place_formatted")] string? PlaceFormatted);
}

/// <summary>Mapbox first; Photon (OpenStreetMap, no key) when Mapbox isn't configured or fails.</summary>
internal sealed class FallbackGeocoder(MapboxGeocoder primary, PhotonGeocoder secondary, MapboxOptions options) : IGeocoder
{
    public async Task<Result<IReadOnlyList<Place>>> SearchAsync(string text, GeoPoint? near, CancellationToken cancellationToken)
    {
        if (options.Configured && await primary.SearchAsync(text, near, cancellationToken) is { IsSuccess: true } found)
        {
            return found;
        }

        return await secondary.SearchAsync(text, near, cancellationToken);
    }
}
