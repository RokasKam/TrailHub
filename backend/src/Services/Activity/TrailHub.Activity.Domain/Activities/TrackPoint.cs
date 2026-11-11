using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Domain.Activities;

public sealed record TrackPoint(double Latitude, double Longitude, double? Elevation, DateTime TimestampUtc)
{
    public GeoPoint ToGeoPoint() => new(Latitude, Longitude, Elevation);
}
