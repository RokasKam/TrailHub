namespace TrailHub.Groups.Application.Requests;

public sealed record ShareLocationRequest(double Latitude, double Longitude, double? Elevation, double? AccuracyMeters, double? HeadingDegrees);
