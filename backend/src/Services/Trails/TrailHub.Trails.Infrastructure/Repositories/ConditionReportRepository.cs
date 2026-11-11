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

internal sealed class ConditionReportRepository(TrailsDbContext db) : IConditionReportRepository
{
    public void Add(ConditionReport report) => db.ConditionReports.Add(report);

    public async Task<IReadOnlyList<ConditionReport>> GetRecentAsync(Guid trailId, int take, CancellationToken cancellationToken) =>
        await db.ConditionReports.AsNoTracking()
            .Where(r => r.TrailId == trailId)
            .OrderByDescending(r => r.ReportedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
}
