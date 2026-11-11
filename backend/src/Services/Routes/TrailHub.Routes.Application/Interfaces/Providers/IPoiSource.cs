using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Interfaces.Providers;

/// <summary>Water, campsites, huts, hotels and viewpoints from OpenStreetMap.</summary>
public interface IPoiSource
{
    /// <summary>Points of interest within <paramref name="corridorMeters"/> of the line.</summary>
    Task<Result<IReadOnlyList<Poi>>> AlongAsync(IReadOnlyList<GeoPoint> line, double corridorMeters, IReadOnlyCollection<PoiKind> kinds, CancellationToken cancellationToken);
}
