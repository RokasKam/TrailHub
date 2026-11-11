using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Application.Responses;

/// <summary>A shared hike with its comments (oldest first) and elevation profile (privacy applied; empty for StatsOnly).</summary>
public sealed record FeedPostDetailsResponse(
    Guid ActivityId,
    FeedUserResponse Author,
    string Name,
    string? Caption,
    DateTime StartedAtUtc,
    DateTime SharedAtUtc,
    double DistanceMeters,
    double ElevationGainMeters,
    long MovingDurationSeconds,
    TrackPrivacy Privacy,
    IReadOnlyList<double[]> Track,
    IReadOnlyList<HikePhotoResponse> Photos,
    IReadOnlyList<FeedGroupResponse> Groups,
    int LikeCount,
    bool LikedByMe,
    int CommentCount,
    bool IsMine,
    IReadOnlyList<CommentResponse> Comments,
    IReadOnlyList<ElevationSample> ElevationProfile);
