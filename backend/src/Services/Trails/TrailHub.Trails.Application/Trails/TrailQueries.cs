using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Application.Abstractions;
using TrailHub.Trails.Application.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Trails;

public sealed record SearchTrailsQuery(TrailSearchCriteria Criteria, PageRequest Page) : IQuery<PagedResult<TrailSummaryDto>>;

internal sealed class SearchTrailsHandler(ITrailRepository trails) : IQueryHandler<SearchTrailsQuery, PagedResult<TrailSummaryDto>>
{
    public async Task<Result<PagedResult<TrailSummaryDto>>> Handle(SearchTrailsQuery query, CancellationToken cancellationToken)
    {
        var c = query.Criteria;
        if ((c.NearLatitude is null) != (c.NearLongitude is null))
        {
            return Error.Validation("Search.IncompleteLocation", "Provide both latitude and longitude.");
        }

        if (c.NearLatitude is { } lat && GeoPoint.Create(lat, c.NearLongitude!.Value).Error is { } invalid)
        {
            return invalid;
        }

        if (c.RadiusKm is <= 0 or > 500)
        {
            return Error.Validation("Search.InvalidRadius", "Radius must be between 0 and 500 km.");
        }

        if (c.Sort == TrailSort.Distance && c.NearLatitude is null)
        {
            return Error.Validation("Search.DistanceSortNeedsLocation", "Sorting by distance requires a location.");
        }

        return await trails.SearchAsync(c, query.Page, cancellationToken);
    }
}

public sealed record GetTrailDetailsQuery(Guid TrailId) : IQuery<TrailDetailsDto>;

internal sealed class GetTrailDetailsHandler(
    ITrailRepository trails,
    IFavoriteTrailRepository favorites,
    IConditionReportRepository conditions,
    IPhotoStorage photos,
    IModerators moderators,
    ICurrentUser user)
    : IQueryHandler<GetTrailDetailsQuery, TrailDetailsDto>
{
    public async Task<Result<TrailDetailsDto>> Handle(GetTrailDetailsQuery query, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(query.TrailId, cancellationToken);
        if (trail is null || !trail.CanSee(user.UserId, moderators))
        {
            return TrailErrors.NotFound;
        }

        var isFavorite = await favorites.GetAsync(user.UserId, trail.Id, cancellationToken) is not null;
        var recent = await conditions.GetRecentAsync(trail.Id, 1, cancellationToken);
        var latest = recent.Count > 0 ? recent[0] : null;

        return new TrailDetailsDto(
            trail.Id,
            trail.Name,
            trail.Description,
            trail.Region,
            trail.Difficulty,
            trail.DistanceMeters,
            trail.ElevationGainMeters,
            trail.ElevationLossMeters,
            trail.MaxElevationMeters,
            trail.EstimatedDurationMinutes,
            trail.IsLoop,
            new LocationDto(trail.Start.Latitude, trail.Start.Longitude),
            trail.Bounds,
            trail.AverageRating,
            trail.ReviewCount,
            isFavorite,
            trail.CreatedBy,
            trail.CreatedAtUtc,
            trail.PointsOfInterest.Select(p => p.ToDto()).ToList(),
            trail.Photos.Select(p => new TrailPhotoDto(photos.ResolveUrl(p.Url), p.Caption, p.UploadedAtUtc)).ToList(),
            latest?.ToDto(),
            trail.HiddenBy,
            moderators.IsModerator(user.UserId));
    }
}

/// <param name="MaxPoints">Optional cap for map previews; full resolution when omitted.</param>
public sealed record GetTrailRouteQuery(Guid TrailId, int? MaxPoints) : IQuery<TrailRouteDto>;

internal sealed class GetTrailRouteHandler(ITrailRepository trails, IModerators moderators, ICurrentUser user) : IQueryHandler<GetTrailRouteQuery, TrailRouteDto>
{
    public async Task<Result<TrailRouteDto>> Handle(GetTrailRouteQuery query, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(query.TrailId, cancellationToken);
        if (trail is null || !trail.CanSee(user.UserId, moderators))
        {
            return TrailErrors.NotFound;
        }

        var points = RouteSampling.Downsample(trail.Route, query.MaxPoints)
            .Select(p => new RoutePointDto(p.Latitude, p.Longitude, p.Elevation))
            .ToList();
        return new TrailRouteDto(trail.Id, trail.Route.Count, points);
    }
}

public sealed record GetElevationProfileQuery(Guid TrailId, int Samples = 200) : IQuery<IReadOnlyList<ElevationSample>>;

internal sealed class GetElevationProfileHandler(ITrailRepository trails, IModerators moderators, ICurrentUser user) : IQueryHandler<GetElevationProfileQuery, IReadOnlyList<ElevationSample>>
{
    public async Task<Result<IReadOnlyList<ElevationSample>>> Handle(GetElevationProfileQuery query, CancellationToken cancellationToken)
    {
        if (query.Samples is < 2 or > 2000)
        {
            return Error.Validation("ElevationProfile.InvalidSamples", "Samples must be between 2 and 2000.");
        }

        var trail = await trails.GetByIdAsync(query.TrailId, cancellationToken);
        return trail is null || !trail.CanSee(user.UserId, moderators) ? TrailErrors.NotFound : Result<IReadOnlyList<ElevationSample>>.Success(trail.GetElevationProfile(query.Samples));
    }
}

public sealed record ExportTrailGpxQuery(Guid TrailId) : IQuery<GpxFileDto>;

internal sealed class ExportTrailGpxHandler(ITrailRepository trails, IGpxSerializer gpx, IModerators moderators, ICurrentUser user) : IQueryHandler<ExportTrailGpxQuery, GpxFileDto>
{
    public async Task<Result<GpxFileDto>> Handle(ExportTrailGpxQuery query, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(query.TrailId, cancellationToken);
        if (trail is null || !trail.CanSee(user.UserId, moderators))
        {
            return TrailErrors.NotFound;
        }

        var document = new GpxDocument(
            trail.Name,
            trail.Description,
            [trail.Route.Select(p => new GpxTrackPoint(p.Latitude, p.Longitude, p.Elevation, null)).ToList()],
            trail.PointsOfInterest
                .Select(p => new GpxWaypoint(p.Name, p.Type.ToString(), p.Description, p.Location.Latitude, p.Location.Longitude, p.Location.Elevation))
                .ToList());
        return new GpxFileDto(TrailMappings.ToFileName(trail.Name), gpx.Write(document));
    }
}
