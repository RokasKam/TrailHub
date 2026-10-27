using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Application.Activities;

public sealed record GetActivitiesQuery(PageRequest Page) : IQuery<PagedResult<ActivitySummaryDto>>;

internal sealed class GetActivitiesHandler(IActivityRepository activities, ICurrentUser user)
    : IQueryHandler<GetActivitiesQuery, PagedResult<ActivitySummaryDto>>
{
    public async Task<Result<PagedResult<ActivitySummaryDto>>> Handle(GetActivitiesQuery query, CancellationToken cancellationToken) =>
        await activities.GetPageAsync(user.UserId, query.Page, cancellationToken);
}

public sealed record GetActivityQuery(Guid Id) : IQuery<ActivityDetailsDto>;

internal sealed class GetActivityHandler(IActivityRepository activities, ICurrentUser user) : IQueryHandler<GetActivityQuery, ActivityDetailsDto>
{
    public async Task<Result<ActivityDetailsDto>> Handle(GetActivityQuery query, CancellationToken cancellationToken)
    {
        var a = await activities.GetByIdAsync(query.Id, cancellationToken);
        if (a is null || a.UserId != user.UserId)
        {
            return ActivityErrors.NotFound;
        }

        var points = a.AllPoints.Select(p => p.ToGeoPoint()).ToList();
        return new ActivityDetailsDto(
            a.Id,
            a.Name,
            a.Notes,
            a.TrailId,
            a.Source,
            a.StartedAtUtc,
            a.EndedAtUtc,
            a.DistanceMeters,
            a.ElevationGainMeters,
            a.ElevationLossMeters,
            a.MaxElevationMeters,
            a.MovingDurationSeconds,
            a.ElapsedDurationSeconds,
            a.AverageSpeedMetersPerSecond,
            GeoBounds.From(points),
            a.Segments.Select(s => (IReadOnlyList<TrackPointDto>)s.Points.Select(p => new TrackPointDto(p.Latitude, p.Longitude, p.Elevation, p.TimestampUtc)).ToList()).ToList(),
            ElevationProfile.Build(a.Segments.Select(s => (IReadOnlyList<GeoPoint>)s.Points.Select(p => p.ToGeoPoint()).ToList()).ToList()));
    }
}

public sealed record ExportActivityGpxQuery(Guid Id) : IQuery<(string FileName, string Content)>;

internal sealed class ExportActivityGpxHandler(IActivityRepository activities, IGpxSerializer gpx, ICurrentUser user)
    : IQueryHandler<ExportActivityGpxQuery, (string FileName, string Content)>
{
    public async Task<Result<(string FileName, string Content)>> Handle(ExportActivityGpxQuery query, CancellationToken cancellationToken)
    {
        var a = await activities.GetByIdAsync(query.Id, cancellationToken);
        if (a is null || a.UserId != user.UserId)
        {
            return ActivityErrors.NotFound;
        }

        var document = new GpxDocument(
            a.Name,
            a.Notes,
            a.Segments.Select(s => (IReadOnlyList<GpxTrackPoint>)s.Points.Select(p => new GpxTrackPoint(p.Latitude, p.Longitude, p.Elevation, p.TimestampUtc)).ToList()).ToList(),
            []);
        return ($"hike-{a.StartedAtUtc:yyyy-MM-dd}-{a.Id.ToString()[..8]}.gpx", gpx.Write(document));
    }
}

public sealed record GetStatisticsQuery : IQuery<HikingStatisticsDto>;

internal sealed class GetStatisticsHandler(IActivityRepository activities, ICurrentUser user, TimeProvider clock)
    : IQueryHandler<GetStatisticsQuery, HikingStatisticsDto>
{
    public async Task<Result<HikingStatisticsDto>> Handle(GetStatisticsQuery query, CancellationToken cancellationToken)
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
            .Select(m => monthly.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month) ?? new MonthlyStatisticsDto(m.Year, m.Month, 0, 0, 0))
            .ToList();

        return new HikingStatisticsDto(allTime, thisYear, lastTwelve);
    }
}
