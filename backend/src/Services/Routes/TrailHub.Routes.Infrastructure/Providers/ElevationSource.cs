using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Infrastructure.Providers.Mapbox;
using TrailHub.Routes.Infrastructure.Providers.OpenMeteo;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>Mapbox terrain first; Open-Meteo when there's no Mapbox token or the tiles can't be fetched.</summary>
internal sealed class ElevationSource(MapboxTerrainElevation mapbox, OpenMeteoElevation openMeteo, MapboxOptions options) : IElevationSource
{
    public async Task<List<GeoPoint>?> TryAddElevationAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken) =>
        (options.Configured ? await mapbox.TryAddElevationAsync(points, cancellationToken) : null)
        ?? await openMeteo.TryAddElevationAsync(points, cancellationToken);
}
