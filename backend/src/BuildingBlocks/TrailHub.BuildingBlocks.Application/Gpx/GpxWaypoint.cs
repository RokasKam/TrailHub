using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.BuildingBlocks.Application.Gpx;

public sealed record GpxWaypoint(string? Name, string? Type, string? Description, double Latitude, double Longitude, double? Elevation);
