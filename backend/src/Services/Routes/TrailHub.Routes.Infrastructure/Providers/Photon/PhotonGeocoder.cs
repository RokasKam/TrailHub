using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Infrastructure.Providers.Photon;

/// <summary>
/// Place search with <see href="https://photon.komoot.io">Photon</see> (OpenStreetMap data, no key). Results near
/// <c>near</c> rank higher, so "Zermatt" finds the village, not a street named after it.
/// </summary>
internal sealed class PhotonGeocoder(HttpClient http, ILogger<PhotonGeocoder> logger) : IGeocoder
{
    public async Task<Result<IReadOnlyList<Place>>> SearchAsync(string text, GeoPoint? near, CancellationToken cancellationToken)
    {
        var url = $"api/?q={Uri.EscapeDataString(text)}&limit=8&lang=en";
        if (near is not null)
        {
            url += string.Create(CultureInfo.InvariantCulture, $"&lat={near.Latitude:F4}&lon={near.Longitude:F4}");
        }

        try
        {
            var response = await http.GetFromJsonAsync<PhotonFeatureCollection>(url, cancellationToken);
            var places = (response?.Features ?? [])
                .Where(f => f.Properties.Name is not null && f.Geometry.Coordinates.Length >= 2)
                .Select(f => new Place(
                    f.Properties.Name!,
                    string.Join(", ", new[] { f.Properties.City, f.Properties.State, f.Properties.Country }.Where(s => !string.IsNullOrEmpty(s) && s != f.Properties.Name).Distinct()) is { Length: > 0 } region ? region : null,
                    f.Geometry.Coordinates[1],
                    f.Geometry.Coordinates[0]))
                .DistinctBy(p => (p.Name, p.Region))
                .ToList();
            return places;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Photon search failed");
            return RouteErrors.RouterUnavailable;
        }
    }

    public async Task<Place?> ReverseAsync(GeoPoint point, CancellationToken cancellationToken)
    {
        try
        {
            var response = await http.GetFromJsonAsync<PhotonFeatureCollection>(string.Create(CultureInfo.InvariantCulture, $"reverse?lat={point.Latitude:F5}&lon={point.Longitude:F5}&lang=en"), cancellationToken);
            return response?.Features is [var f, ..] && (f.Properties.City ?? f.Properties.Name) is { } name
                ? new Place(name, f.Properties.State, point.Latitude, point.Longitude)
                : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Photon reverse lookup failed");
            return null;
        }
    }
}
