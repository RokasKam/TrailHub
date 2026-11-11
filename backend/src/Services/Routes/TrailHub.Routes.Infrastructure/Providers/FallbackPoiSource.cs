using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Domain.Routes;
using TrailHub.Routes.Infrastructure.Providers.Mapbox;
using TrailHub.Routes.Infrastructure.Providers.Overpass;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>Mapbox first (fast, reliable); OpenStreetMap's Overpass when Mapbox isn't configured or fails.</summary>
internal sealed class FallbackPoiSource(MapboxPoiSource primary, OverpassPoiSource secondary, MapboxOptions options, ILogger<FallbackPoiSource> logger) : IPoiSource
{
    public async Task<Result<IReadOnlyList<Poi>>> AlongAsync(IReadOnlyList<GeoPoint> line, double corridorMeters, IReadOnlyCollection<PoiKind> kinds, CancellationToken cancellationToken)
    {
        if (options.Configured)
        {
            var result = await primary.AlongAsync(line, corridorMeters, kinds, cancellationToken);
            if (result.IsSuccess)
            {
                return result;
            }

            logger.LogInformation("Mapbox points of interest unavailable; trying OpenStreetMap Overpass");
        }

        return await secondary.AlongAsync(line, corridorMeters, kinds, cancellationToken);
    }
}
