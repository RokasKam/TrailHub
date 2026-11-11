using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Mappings;

internal static class TrailMappings
{
    public static PointOfInterestResponse ToResponse(this PointOfInterest p) =>
        new(p.Id, p.Name, p.Type, p.Location.Latitude, p.Location.Longitude, p.Location.Elevation, p.Description);

    public static ConditionReportResponse ToResponse(this ConditionReport r) => new(r.Id, r.Condition, r.Note, r.UserId, r.ReportedAtUtc);

    public static string ToFileName(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
        return $"{(safe.Length == 0 ? "route" : safe)}.gpx";
    }
}
