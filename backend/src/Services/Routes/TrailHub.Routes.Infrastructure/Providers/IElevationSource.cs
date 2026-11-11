using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>Adds ground elevation to a line.</summary>
internal interface IElevationSource
{
    /// <summary>The points with elevation, or null if it couldn't be looked up.</summary>
    Task<List<GeoPoint>?> TryAddElevationAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken);
}
