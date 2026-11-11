using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class FavoriteTrailService(
    ITrailRepository trails,
    IFavoriteTrailRepository favorites,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IFavoriteTrailService
{
    public async Task<IReadOnlyList<TrailSummaryResponse>> GetFavoritesAsync(CancellationToken cancellationToken)
    {
        var ids = await favorites.GetTrailIdsAsync(user.UserId, cancellationToken);
        return ids.Count == 0 ? [] : await trails.GetSummariesAsync(ids, cancellationToken);
    }

    public async Task SetFavoriteAsync(Guid trailId, bool isFavorite, CancellationToken cancellationToken)
    {
        var existing = await favorites.GetAsync(user.UserId, trailId, cancellationToken);
        if (isFavorite && existing is null)
        {
            if (!await trails.ExistsAsync(trailId, cancellationToken))
            {
                throw TrailErrors.NotFound.ToException();
            }

            favorites.Add(FavoriteTrail.Create(user.UserId, trailId, clock.GetUtcNow().UtcDateTime));
        }
        else if (!isFavorite && existing is not null)
        {
            favorites.Remove(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
