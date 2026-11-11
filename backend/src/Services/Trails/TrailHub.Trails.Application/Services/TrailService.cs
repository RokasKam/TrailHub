using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Application.Extensions;
using TrailHub.Trails.Application.Interfaces.Providers;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Mappings;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class TrailService(
    ITrailRepository trails,
    IFavoriteTrailRepository favorites,
    IConditionReportRepository conditions,
    IPhotoStorage photos,
    IModerators moderators,
    IGpxSerializer gpx,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : ITrailService
{
    public async Task<PagedResult<TrailSummaryResponse>> SearchAsync(TrailSearchCriteria criteria, PageRequest page, CancellationToken cancellationToken)
    {
        var c = criteria;
        if ((c.NearLatitude is null) != (c.NearLongitude is null))
        {
            throw Error.Validation("Search.IncompleteLocation", "Provide both latitude and longitude.").ToException();
        }

        if (c.NearLatitude is { } lat && GeoPoint.Create(lat, c.NearLongitude!.Value).Error is { } invalid)
        {
            throw invalid.ToException();
        }

        if (c.RadiusKm is <= 0 or > 500)
        {
            throw Error.Validation("Search.InvalidRadius", "Radius must be between 0 and 500 km.").ToException();
        }

        if (c.Sort == TrailSort.Distance && c.NearLatitude is null)
        {
            throw Error.Validation("Search.DistanceSortNeedsLocation", "Sorting by distance requires a location.").ToException();
        }

        return await trails.SearchAsync(c, page, cancellationToken);
    }

    public async Task<TrailDetailsResponse> GetTrailAsync(Guid trailId, CancellationToken cancellationToken)
    {
        var trail = await GetVisibleAsync(trailId, cancellationToken);
        var isFavorite = await favorites.GetAsync(user.UserId, trail.Id, cancellationToken) is not null;
        var recent = await conditions.GetRecentAsync(trail.Id, 1, cancellationToken);
        var latest = recent.Count > 0 ? recent[0] : null;

        return new TrailDetailsResponse(
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
            new LocationResponse(trail.Start.Latitude, trail.Start.Longitude),
            trail.Bounds,
            trail.AverageRating,
            trail.ReviewCount,
            isFavorite,
            trail.CreatedBy,
            trail.CreatedAtUtc,
            trail.PointsOfInterest.Select(p => p.ToResponse()).ToList(),
            trail.Photos.Select(p => new TrailPhotoResponse(photos.ResolveUrl(p.Url), p.Caption, p.UploadedAtUtc)).ToList(),
            latest?.ToResponse(),
            trail.HiddenBy,
            moderators.IsModerator(user.UserId));
    }

    public async Task<TrailRouteResponse> GetRouteAsync(Guid trailId, int? maxPoints, CancellationToken cancellationToken)
    {
        var trail = await GetVisibleAsync(trailId, cancellationToken);
        var points = RouteSampling.Downsample(trail.Route, maxPoints)
            .Select(p => new RoutePointResponse(p.Latitude, p.Longitude, p.Elevation))
            .ToList();
        return new TrailRouteResponse(trail.Id, trail.Route.Count, points);
    }

    public async Task<IReadOnlyList<ElevationSample>> GetElevationProfileAsync(Guid trailId, int samples, CancellationToken cancellationToken)
    {
        if (samples is < 2 or > 2000)
        {
            throw Error.Validation("ElevationProfile.InvalidSamples", "Samples must be between 2 and 2000.").ToException();
        }

        var trail = await GetVisibleAsync(trailId, cancellationToken);
        return trail.GetElevationProfile(samples);
    }

    public async Task<GpxFileResponse> ExportGpxAsync(Guid trailId, CancellationToken cancellationToken)
    {
        var trail = await GetVisibleAsync(trailId, cancellationToken);
        var document = new GpxDocument(
            trail.Name,
            trail.Description,
            [trail.Route.Select(p => new GpxTrackPoint(p.Latitude, p.Longitude, p.Elevation, null)).ToList()],
            trail.PointsOfInterest
                .Select(p => new GpxWaypoint(p.Name, p.Type.ToString(), p.Description, p.Location.Latitude, p.Location.Longitude, p.Location.Elevation))
                .ToList());
        return new GpxFileResponse(TrailMappings.ToFileName(trail.Name), gpx.Write(document));
    }

    public async Task<CreatedTrailResponse> ImportGpxAsync(Stream gpxFile, string? name, string? description, string region, Difficulty difficulty, CancellationToken cancellationToken)
    {
        var document = gpx.Parse(gpxFile).GetValueOrThrow();
        var route = new List<GeoPoint>();
        foreach (var point in document.AllPoints)
        {
            route.Add(GeoPoint.Create(point.Latitude, point.Longitude, point.Elevation).GetValueOrThrow());
        }

        var trail = Trail.Create(
            name ?? document.Name ?? "Imported trail",
            description ?? document.Description,
            region,
            difficulty,
            route,
            user.UserId,
            clock.GetUtcNow().UtcDateTime).GetValueOrThrow();

        foreach (var waypoint in document.Waypoints.Take(Trail.MaxPointsOfInterest))
        {
            var location = GeoPoint.Create(waypoint.Latitude, waypoint.Longitude, waypoint.Elevation).GetValueOrThrow();
            var type = PointOfInterest.InferType($"{waypoint.Type} {waypoint.Name}");
            trail.AddPointOfInterest(waypoint.Name ?? type.ToString(), type, location, waypoint.Description).GetValueOrThrow();
        }

        trails.Add(trail);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreatedTrailResponse(trail.Id);
    }

    public async Task UpdateAsync(Guid trailId, UpdateTrailRequest request, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        trail.UpdateDetails(request.Name, request.Description, request.Region, request.Difficulty, user.UserId).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The trail, if the caller may see it; hidden trails are reported as not found to everyone else.</summary>
    private async Task<Trail> GetVisibleAsync(Guid trailId, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(trailId, cancellationToken);
        return trail is not null && trail.CanSee(user.UserId, moderators)
            ? trail
            : throw TrailErrors.NotFound.ToException();
    }
}
