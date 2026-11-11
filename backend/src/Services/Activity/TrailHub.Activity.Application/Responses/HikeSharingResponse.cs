using TrailHub.Activity.Domain.Activities;

namespace TrailHub.Activity.Application.Responses;

/// <summary>Who a hike is shared with and how.</summary>
public sealed record HikeSharingResponse(IReadOnlyList<Guid> GroupIds, string? Caption, TrackPrivacy Privacy, DateTime SharedAtUtc);
