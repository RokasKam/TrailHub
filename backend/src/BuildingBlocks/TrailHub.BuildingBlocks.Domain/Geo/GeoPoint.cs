namespace TrailHub.BuildingBlocks.Domain.Geo;

/// <summary>A WGS84 coordinate with optional elevation in meters.</summary>
public sealed record GeoPoint
{
    public GeoPoint(double latitude, double longitude, double? elevation = null)
    {
        if (latitude is < -90 or > 90 || double.IsNaN(latitude))
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180 || double.IsNaN(longitude))
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180.");
        }

        Latitude = latitude;
        Longitude = longitude;
        Elevation = elevation;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public double? Elevation { get; }

    public static Result<GeoPoint> Create(double latitude, double longitude, double? elevation = null)
    {
        if (latitude is < -90 or > 90 || double.IsNaN(latitude) ||
            longitude is < -180 or > 180 || double.IsNaN(longitude))
        {
            return Error.Validation("Geo.InvalidCoordinate", $"({latitude}, {longitude}) is not a valid coordinate.");
        }

        return new GeoPoint(latitude, longitude, elevation);
    }
}
