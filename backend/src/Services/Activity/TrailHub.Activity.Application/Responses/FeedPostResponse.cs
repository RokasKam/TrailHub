using TrailHub.Activity.Domain.Activities;

namespace TrailHub.Activity.Application.Responses;

/// <summary>A shared hike in the feed.</summary>
/// <param name="Track">[lat, lon] pairs, privacy applied and simplified to at most 300 points; empty for StatsOnly.</param>
public sealed record FeedPostResponse(
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
    bool IsMine);
