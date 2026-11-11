namespace TrailHub.Groups.Application.Dtos;

/// <summary>A member's latest shared position: returned by the API, pushed over SignalR and stored in the live-location cache.</summary>
public sealed record LiveLocationDto(
    string UserId,
    double Latitude,
    double Longitude,
    double? Elevation,
    double? AccuracyMeters,
    double? HeadingDegrees,
    DateTime RecordedAtUtc);
