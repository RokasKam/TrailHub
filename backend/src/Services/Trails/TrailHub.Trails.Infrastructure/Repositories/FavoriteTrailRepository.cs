using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trails.Infrastructure.Persistence;

namespace TrailHub.Trails.Infrastructure.Repositories;

internal sealed class FavoriteTrailRepository(TrailsDbContext db) : IFavoriteTrailRepository
{
    public Task<FavoriteTrail?> GetAsync(string userId, Guid trailId, CancellationToken cancellationToken) =>
        db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.TrailId == trailId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetTrailIdsAsync(string userId, CancellationToken cancellationToken) =>
        await db.Favorites.Where(f => f.UserId == userId).OrderByDescending(f => f.SavedAtUtc).Select(f => f.TrailId).ToListAsync(cancellationToken);

    public void Add(FavoriteTrail favorite) => db.Favorites.Add(favorite);

    public void Remove(FavoriteTrail favorite) => db.Favorites.Remove(favorite);
}
