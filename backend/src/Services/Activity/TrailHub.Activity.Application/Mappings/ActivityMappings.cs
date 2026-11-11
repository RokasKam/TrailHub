using TrailHub.Activity.Application.Dtos;
using TrailHub.Activity.Application.Interfaces.Providers;
using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Application.Mappings;

internal static class ActivityMappings
{
    public static ActivitySummaryResponse ToSummary(this HikeActivity a) =>
        new(a.Id, a.Name, a.TrailId, a.Source, a.StartedAtUtc, a.EndedAtUtc, a.DistanceMeters, a.ElevationGainMeters, a.MovingDurationSeconds, a.AverageSpeedMetersPerSecond);

    public static ActivityDetailsResponse ToDetails(this HikeActivity a, IHikePhotoStorage storage)
    {
        var points = a.AllPoints.Select(p => p.ToGeoPoint()).ToList();
        return new ActivityDetailsResponse(
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
            ElevationProfile.Build(a.Segments.Select(s => (IReadOnlyList<GeoPoint>)s.Points.Select(p => p.ToGeoPoint()).ToList()).ToList()),
            a.RouteId,
            a.ToSharing(),
            a.Photos.Select(p => p.ToResponse(storage)).ToList());
    }

    public static HikeSharingResponse? ToSharing(this HikeActivity a) =>
        a.SharedAtUtc is { } sharedAt ? new HikeSharingResponse(a.SharedGroupIds, a.Caption, a.Privacy, sharedAt) : null;

    /// <summary>A photo as <paramref name="viewerIsAuthor"/> (the hike's author, by default) or someone else sees it.</summary>
    public static HikePhotoResponse ToResponse(this HikePhoto p, IHikePhotoStorage storage, bool viewerIsAuthor = true, bool viewerIsModerator = false) =>
        new(p.Id, storage.ResolveUrl(p.Key), p.Caption, p.AddedAtUtc, p.HiddenBy is not null, !viewerIsAuthor, viewerIsAuthor || viewerIsModerator);
}
