using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Requests;

public sealed record AddPointOfInterestRequest(string Name, PointOfInterestType Type, double Latitude, double Longitude, double? Elevation, string? Description);
