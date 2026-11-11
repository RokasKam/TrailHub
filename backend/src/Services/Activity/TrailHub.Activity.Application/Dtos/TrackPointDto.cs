namespace TrailHub.Activity.Application.Dtos;

/// <summary>A GPS fix, sent when recording and returned with a hike's track.</summary>
public sealed record TrackPointDto(double Latitude, double Longitude, double? Elevation, DateTime TimestampUtc);
