using Microsoft.EntityFrameworkCore;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Infrastructure.Persistence;

namespace TrailHub.Trails.Infrastructure.Repositories;

internal sealed class PhotoReportRepository(TrailsDbContext db) : IPhotoReportRepository
{
    public Task<PhotoReport?> GetByUserAsync(Guid photoId, string userId, CancellationToken cancellationToken) =>
        db.PhotoReports.FirstOrDefaultAsync(r => r.PhotoId == photoId && r.UserId == userId, cancellationToken);

    public Task<int> CountOpenAsync(Guid photoId, CancellationToken cancellationToken) =>
        db.PhotoReports.CountAsync(r => r.PhotoId == photoId && r.ResolvedAtUtc == null, cancellationToken);

    public async Task<IReadOnlyList<PhotoReport>> GetOpenAsync(Guid photoId, CancellationToken cancellationToken) =>
        await db.PhotoReports.Where(r => r.PhotoId == photoId && r.ResolvedAtUtc == null).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PhotoReport>> GetAllAsync(Guid photoId, CancellationToken cancellationToken) =>
        await db.PhotoReports.Where(r => r.PhotoId == photoId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PhotoModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken)
    {
        var open = await db.PhotoReports.AsNoTracking().Where(r => r.ResolvedAtUtc == null).ToListAsync(cancellationToken);
        var reportedPhotoIds = open.Select(r => r.PhotoId).Distinct().ToList();
        var reportedTrailIds = open.Select(r => r.TrailId).Distinct().ToList();
        var trails = await db.Trails.AsNoTracking()
            .Where(t => reportedTrailIds.Contains(t.Id) || t.Photos.Any(p => p.HiddenBy != null))
            .Select(t => new
            {
                t.Id,
                t.Name,
                Photos = t.Photos
                    .Where(p => p.HiddenBy != null || reportedPhotoIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Url, p.Caption, p.UploadedBy, p.HiddenBy })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return trails
            .SelectMany(t => t.Photos.Select(p =>
            {
                var reports = open.Where(r => r.PhotoId == p.Id).OrderByDescending(r => r.CreatedAtUtc).ToList();
                return new PhotoModerationItemResponse(
                    t.Id,
                    t.Name,
                    p.Id,
                    p.Url,
                    p.Caption,
                    p.UploadedBy,
                    p.HiddenBy,
                    reports.Count,
                    reports.Select(r => new PhotoReportResponse(r.UserId, r.Reason, r.Details, r.CreatedAtUtc)).ToList());
            }))
            .OrderByDescending(i => i.OpenReports)
            .ThenBy(i => i.TrailName)
            .ToList();
    }

    public void Add(PhotoReport report) => db.PhotoReports.Add(report);

    public void Remove(PhotoReport report) => db.PhotoReports.Remove(report);
}
