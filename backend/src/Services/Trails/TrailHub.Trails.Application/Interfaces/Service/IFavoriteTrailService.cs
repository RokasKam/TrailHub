using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>The caller's favorite trails.</summary>
public interface IFavoriteTrailService
{
    Task<IReadOnlyList<TrailSummaryResponse>> GetFavoritesAsync(CancellationToken cancellationToken);

    /// <summary>Idempotent: favoriting twice or un-favoriting a non-favorite both succeed.</summary>
    Task SetFavoriteAsync(Guid trailId, bool isFavorite, CancellationToken cancellationToken);
}
