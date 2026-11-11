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

internal sealed class ReviewRepository(TrailsDbContext db) : IReviewRepository
{
    public Task<Review?> GetByUserAsync(Guid trailId, string userId, CancellationToken cancellationToken) =>
        db.Reviews.FirstOrDefaultAsync(r => r.TrailId == trailId && r.UserId == userId, cancellationToken);

    public void Add(Review review) => db.Reviews.Add(review);

    public async Task<PagedResult<Review>> GetPageAsync(Guid trailId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Reviews.AsNoTracking().Where(r => r.TrailId == trailId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.CreatedAtUtc).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<Review>(items, page.Page, page.PageSize, total);
    }

    public async Task<(int Sum, int Count)> GetRatingTotalsAsync(Guid trailId, string excludingUserId, CancellationToken cancellationToken)
    {
        var totals = await db.Reviews
            .Where(r => r.TrailId == trailId && r.UserId != excludingUserId)
            .GroupBy(_ => 1)
            .Select(g => new { Sum = g.Sum(r => r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);
        return totals is null ? (0, 0) : (totals.Sum, totals.Count);
    }
}
