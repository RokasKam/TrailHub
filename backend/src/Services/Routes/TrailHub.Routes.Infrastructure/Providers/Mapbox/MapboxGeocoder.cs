using System.Globalization;
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
/// Place search with the <see href="https://docs.mapbox.com/api/search/search-box/">Mapbox Search Box API</see>:
/// towns and also points of interest such as huts, so "Cabane des Vignettes" finds the hut. Results near
/// <c>near</c> rank higher.
/// </summary>
internal sealed class MapboxGeocoder(HttpClient http, MapboxOptions options, ILogger<MapboxGeocoder> logger) : IGeocoder
{
    public async Task<Result<IReadOnlyList<Place>>> SearchAsync(string text, GeoPoint? near, CancellationToken cancellationToken)
    {
        // auto_complete matches unfinished words, so results appear while the user is still typing.
        var url = $"search/searchbox/v1/forward?q={Uri.EscapeDataString(text)}&limit=8&language=en&auto_complete=true&access_token={options.AccessToken}";
        if (near is not null)
        {
            url += string.Create(CultureInfo.InvariantCulture, $"&proximity={near.Longitude:F4},{near.Latitude:F4}");
        }

        try
        {
            var response = await http.GetFromJsonAsync<MapboxSearchFeatureCollection>(url, cancellationToken);
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

    public async Task<Place?> ReverseAsync(GeoPoint point, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"search/searchbox/v1/reverse?longitude={point.Longitude:F5}&latitude={point.Latitude:F5}&language=en&limit=1&access_token={options.AccessToken}");
        try
        {
            var response = await http.GetFromJsonAsync<MapboxSearchFeatureCollection>(url, cancellationToken);
            if (response?.Features is not [var f, ..])
            {
                return null;
            }

            // A hut or summit is a better name than the street address the lookup often returns.
            var c = f.Properties.Context;
            var name = f.Properties.FeatureType == "poi" ? f.Properties.Name : c?.Locality?.Name ?? c?.Place?.Name ?? f.Properties.Name;
            return name is null ? null : new Place(name, c?.Region?.Name, point.Latitude, point.Longitude);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Mapbox reverse lookup failed");
            return null;
        }
    }
}
