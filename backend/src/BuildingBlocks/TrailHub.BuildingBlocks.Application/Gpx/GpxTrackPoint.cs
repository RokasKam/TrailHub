using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.BuildingBlocks.Application.Gpx;

public sealed record GpxTrackPoint(double Latitude, double Longitude, double? Elevation, DateTime? TimeUtc);
