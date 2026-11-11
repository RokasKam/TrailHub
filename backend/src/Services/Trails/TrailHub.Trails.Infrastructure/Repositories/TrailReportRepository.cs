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

internal sealed class TrailReportRepository(TrailsDbContext db) : ITrailReportRepository
{
    public Task<TrailReport?> GetByUserAsync(Guid trailId, string userId, CancellationToken cancellationToken) =>
        db.TrailReports.FirstOrDefaultAsync(r => r.TrailId == trailId && r.UserId == userId, cancellationToken);

    public Task<int> CountOpenAsync(Guid trailId, CancellationToken cancellationToken) =>
        db.TrailReports.CountAsync(r => r.TrailId == trailId && r.ResolvedAtUtc == null, cancellationToken);

    public async Task<IReadOnlyList<TrailReport>> GetOpenAsync(Guid trailId, CancellationToken cancellationToken) =>
        await db.TrailReports.Where(r => r.TrailId == trailId && r.ResolvedAtUtc == null).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken)
    {
        var open = await db.TrailReports.AsNoTracking().Where(r => r.ResolvedAtUtc == null).ToListAsync(cancellationToken);
        var reportedIds = open.Select(r => r.TrailId).Distinct().ToList();
        var trails = await db.Trails.AsNoTracking()
            .Where(t => reportedIds.Contains(t.Id) || t.HiddenBy == HiddenBy.Reports || t.HiddenBy == HiddenBy.Moderator)
            .Select(t => new { t.Id, t.Name, t.Region, t.CreatedBy, t.HiddenBy })
            .ToListAsync(cancellationToken);
        return trails
            .Select(t =>
            {
                var reports = open.Where(r => r.TrailId == t.Id).OrderByDescending(r => r.CreatedAtUtc).ToList();
                return new ModerationItemResponse(t.Id, t.Name, t.Region, t.CreatedBy, t.HiddenBy, reports.Count, reports.Select(r => new ModerationReportResponse(r.Reason, r.Details, r.CreatedAtUtc)).ToList());
            })
            .OrderByDescending(i => i.OpenReports)
            .ThenBy(i => i.Name)
            .ToList();
    }

    public void Add(TrailReport report) => db.TrailReports.Add(report);
}
