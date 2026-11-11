using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Interfaces.Repository;

public interface IFavoriteTrailRepository
{
    Task<FavoriteTrail?> GetAsync(string userId, Guid trailId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetTrailIdsAsync(string userId, CancellationToken cancellationToken);

    void Add(FavoriteTrail favorite);

    void Remove(FavoriteTrail favorite);
}
