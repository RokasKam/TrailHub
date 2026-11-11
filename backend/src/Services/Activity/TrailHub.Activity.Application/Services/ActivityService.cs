using TrailHub.Activity.Application.Dtos;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Mappings;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.Contracts;

namespace TrailHub.Activity.Application.Services;

internal sealed class ActivityService(
    IActivityRepository activities,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    IGpxSerializer gpx,
    ICurrentUser user,
    TimeProvider clock) : IActivityService
{
    public Task<PagedResult<ActivitySummaryResponse>> GetActivitiesAsync(PageRequest page, CancellationToken cancellationToken) =>
        activities.GetPageAsync(user.UserId, page, cancellationToken);

    public async Task<ActivityDetailsResponse> GetActivityAsync(Guid id, CancellationToken cancellationToken) =>
        (await GetOwnAsync(id, cancellationToken)).ToDetails();

    public async Task<GpxFileResponse> ExportGpxAsync(Guid id, CancellationToken cancellationToken)
    {
        var a = await GetOwnAsync(id, cancellationToken);
        var document = new GpxDocument(
            a.Name,
            a.Notes,
            a.Segments.Select(s => (IReadOnlyList<GpxTrackPoint>)s.Points.Select(p => new GpxTrackPoint(p.Latitude, p.Longitude, p.Elevation, p.TimestampUtc)).ToList()).ToList(),
            []);
        return new GpxFileResponse($"hike-{a.StartedAtUtc:yyyy-MM-dd}-{a.Id.ToString()[..8]}.gpx", gpx.Write(document));
    }

    public async Task<HikingStatisticsResponse> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var twelveMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        var allTime = await activities.GetTotalsAsync(user.UserId, null, cancellationToken);
        var thisYear = await activities.GetTotalsAsync(user.UserId, startOfYear, cancellationToken);
        var monthly = await activities.GetMonthlyAsync(user.UserId, twelveMonthsAgo, cancellationToken);

        // Fill months without hikes so charts get a continuous 12-month axis.
        var lastTwelve = Enumerable.Range(0, 12)
            .Select(i => twelveMonthsAgo.AddMonths(i))
            .Select(m => monthly.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month) ?? new MonthlyStatisticsResponse(m.Year, m.Month, 0, 0, 0))
            .ToList();

        return new HikingStatisticsResponse(allTime, thisYear, lastTwelve);
    }

    public Task<ActivitySummaryResponse> RecordAsync(Guid id, RecordActivityRequest request, CancellationToken cancellationToken)
    {
        var segments = request.Segments
            .Select(s => new ActivitySegment(s.Select(ToTrackPoint).ToList()))
            .ToList();
        return SaveAsync(id, request.Name, request.TrailId, segments, ActivitySource.Recorded, request.Notes, request.RouteId, cancellationToken);
    }

    public async Task<ActivitySummaryResponse> ImportGpxAsync(Stream gpxFile, string? name, Guid? trailId, CancellationToken cancellationToken)
    {
        var parsed = gpx.Parse(gpxFile).GetValueOrThrow();
        if (parsed.AllPoints.Any(p => p.TimeUtc is null))
        {
            throw ActivityErrors.GpxWithoutTimestamps.ToException();
        }

        var segments = parsed.Segments
            .Select(s => new ActivitySegment(s.Select(p => new TrackPoint(p.Latitude, p.Longitude, p.Elevation, p.TimeUtc!.Value)).ToList()))
            .ToList();
        return await SaveAsync(
            Guid.CreateVersion7(),
            name ?? parsed.Name ?? "Imported hike",
            trailId,
            segments,
            ActivitySource.ImportedGpx,
            parsed.Description,
            routeId: null,
            cancellationToken);
    }

    public async Task UpdateAsync(Guid id, UpdateActivityRequest request, CancellationToken cancellationToken)
    {
        var activity = await GetOwnAsync(id, cancellationToken);
        activity.UpdateDetails(request.Name, request.Notes).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var activity = await GetOwnAsync(id, cancellationToken);
        activities.Remove(activity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishAsync(new ActivityDeletedIntegrationEvent(activity.Id, activity.UserId), cancellationToken);
    }

    private static TrackPoint ToTrackPoint(TrackPointDto p) =>
        new(p.Latitude, p.Longitude, p.Elevation, DateTime.SpecifyKind(p.TimestampUtc, DateTimeKind.Utc));

    /// <summary>The caller's hike. Other people's hikes are reported as not found.</summary>
    private async Task<HikeActivity> GetOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        var activity = await activities.GetByIdAsync(id, cancellationToken);
        return activity is not null && activity.UserId == user.UserId
            ? activity
            : throw ActivityErrors.NotFound.ToException();
    }

    private async Task<ActivitySummaryResponse> SaveAsync(
        Guid id,
        string name,
        Guid? trailId,
        IReadOnlyList<ActivitySegment> segments,
        ActivitySource source,
        string? notes,
        Guid? routeId,
        CancellationToken cancellationToken)
    {
        if (await activities.GetByIdAsync(id, cancellationToken) is { } existing)
        {
            return existing.UserId == user.UserId ? existing.ToSummary() : throw ActivityErrors.IdTaken.ToException();
        }

        var activity = HikeActivity.Create(id, user.UserId, name, trailId, segments, source, notes, clock.GetUtcNow().UtcDateTime).GetValueOrThrow();
        activity.FollowedRoute(routeId);
        activities.Add(activity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await events.PublishAsync(
            new ActivityCompletedIntegrationEvent(
                activity.Id,
                activity.UserId,
                activity.TrailId,
                activity.DistanceMeters,
                activity.ElevationGainMeters,
                activity.MovingDurationSeconds,
                activity.EndedAtUtc),
            cancellationToken);

        return activity.ToSummary();
    }
}
