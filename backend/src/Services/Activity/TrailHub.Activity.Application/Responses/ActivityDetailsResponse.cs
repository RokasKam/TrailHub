using TrailHub.Activity.Application.Dtos;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Application.Responses;

public sealed record ActivityDetailsResponse(
    Guid Id,
    string Name,
    string? Notes,
    Guid? TrailId,
    ActivitySource Source,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    double DistanceMeters,
    double ElevationGainMeters,
    double ElevationLossMeters,
    double? MaxElevationMeters,
    long MovingDurationSeconds,
    long ElapsedDurationSeconds,
    double AverageSpeedMetersPerSecond,
    GeoBounds Bounds,
    IReadOnlyList<IReadOnlyList<TrackPointDto>> Segments,
    IReadOnlyList<ElevationSample> ElevationProfile,
    Guid? RouteId);
