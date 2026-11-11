namespace TrailHub.Routes.Application.Responses;

/// <param name="IsOwner">False for routes other people shared with one of your groups.</param>
public sealed record RouteSummaryResponse(
    Guid Id,
    string Name,
    double DistanceMeters,
    double AscentMeters,
    int DurationMinutes,
    int DayCount,
    bool Snapped,
    double StartLatitude,
    double StartLongitude,
    DateTime UpdatedAtUtc,
    string OwnerId,
    bool IsOwner,
    bool Published);
