using TrailHub.Activity.Domain.Activities;

namespace TrailHub.Activity.Application.Requests;

/// <param name="GroupIds">1 to 10 of the caller's groups.</param>
/// <param name="Caption">Optional caption, at most 500 characters.</param>
/// <param name="Privacy">What group members see of the track.</param>
public sealed record ShareHikeRequest(IReadOnlyList<Guid>? GroupIds, string? Caption, TrackPrivacy Privacy);
