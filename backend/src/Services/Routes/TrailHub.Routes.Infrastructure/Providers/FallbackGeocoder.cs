using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Infrastructure.Providers.Mapbox;
using TrailHub.Routes.Infrastructure.Providers.Photon;

namespace TrailHub.Routes.Infrastructure.Providers;

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

    public async Task<Place?> ReverseAsync(GeoPoint point, CancellationToken cancellationToken) =>
        (options.Configured ? await primary.ReverseAsync(point, cancellationToken) : null) ?? await secondary.ReverseAsync(point, cancellationToken);
}
