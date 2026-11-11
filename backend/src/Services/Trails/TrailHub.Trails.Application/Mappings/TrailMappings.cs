using TrailHub.Trails.Application.Interfaces.Providers;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Mappings;

internal static class TrailMappings
{
    public static PointOfInterestResponse ToResponse(this PointOfInterest p) =>
        new(p.Id, p.Name, p.Type, p.Location.Latitude, p.Location.Longitude, p.Location.Elevation, p.Description);

    public static ConditionReportResponse ToResponse(this ConditionReport r) => new(r.Id, r.Condition, r.Note, r.UserId, r.ReportedAtUtc);

    public static TrailPhotoResponse ToResponse(this TrailPhoto p, Trail trail, IPhotoStorage storage, string viewerId, bool viewerIsModerator) =>
        new(p.Id, storage.ResolveUrl(p.Url), p.Caption, p.UploadedAtUtc, p.UploadedBy, p.IsHidden, trail.CanDeletePhoto(p, viewerId, viewerIsModerator));

    public static string ToFileName(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
        return $"{(safe.Length == 0 ? "route" : safe)}.gpx";
    }
}
