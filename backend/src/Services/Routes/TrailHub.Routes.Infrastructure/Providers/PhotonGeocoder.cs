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
            var response = await http.GetFromJsonAsync<FeatureCollection>(url, cancellationToken);
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

    private sealed record FeatureCollection(IReadOnlyList<Feature>? Features);

    private sealed record Feature(Geometry Geometry, Properties Properties);

    private sealed record Geometry(double[] Coordinates);

    private sealed record Properties(string? Name, string? City, string? State, string? Country);
}
