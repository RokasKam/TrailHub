using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Models;

namespace TrailHub.Routes.Application.Interfaces.Providers;

public interface IGeocoder
{
    Task<Result<IReadOnlyList<Place>>> SearchAsync(string text, GeoPoint? near, CancellationToken cancellationToken);

    /// <summary>A short name for a point: the hut or other place of interest there, else the village or town.</summary>
    Task<Place?> ReverseAsync(GeoPoint point, CancellationToken cancellationToken);
}
