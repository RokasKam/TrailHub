using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Responses;

public sealed record PointOfInterestResponse(Guid Id, string Name, PointOfInterestType Type, double Latitude, double Longitude, double? Elevation, string? Description);
