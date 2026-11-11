using TrailHub.Activity.Application.Interfaces.Providers;
using TrailHub.Activity.Application.Models;
using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Application.Mappings;

internal static class FeedMappings
{
    /// <summary>Feed tracks are previews: at most this many points.</summary>
    public const int MaxTrackPoints = 300;

    /// <summary>The track segments <paramref name="viewerId"/> may see: the author sees the full track, others the privacy-trimmed one.</summary>
    public static IReadOnlyList<IReadOnlyList<GeoPoint>> VisibleSegments(this HikeActivity a, string viewerId) =>
        TrackPrivacyFilter.Apply(
            a.Segments.Select(s => (IReadOnlyList<GeoPoint>)s.Points.Select(p => p.ToGeoPoint()).ToList()).ToList(),
            a.UserId == viewerId ? TrackPrivacy.Full : a.Privacy);

    /// <summary>[lat, lon] pairs, simplified to at most <see cref="MaxTrackPoints"/> points.</summary>
    public static IReadOnlyList<double[]> ToPreviewTrack(IReadOnlyList<IReadOnlyList<GeoPoint>> segments) =>
        TrackSimplifier.Simplify(segments.SelectMany(s => s).ToList(), MaxTrackPoints)
            .Select(p => new[] { Math.Round(p.Latitude, 6), Math.Round(p.Longitude, 6) })
            .ToList();

    public static FeedPostResponse ToFeedPost(
        this HikeActivity a,
        string viewerId,
        string authorName,
        IReadOnlyList<GroupInfo> viewerGroups,
        int likeCount,
        bool likedByMe,
        int commentCount,
        IHikePhotoStorage storage) =>
        new(
            a.Id,
            new FeedUserResponse(a.UserId, authorName),
            a.Name,
            a.Caption,
            a.StartedAtUtc,
            a.SharedAtUtc ?? a.CreatedAtUtc,
            a.DistanceMeters,
            a.ElevationGainMeters,
            a.MovingDurationSeconds,
            a.Privacy,
            ToPreviewTrack(a.VisibleSegments(viewerId)),
            a.Photos.Select(p => p.ToResponse(storage)).ToList(),
            viewerGroups.Where(g => a.SharedGroupIds.Contains(g.Id)).Select(g => new FeedGroupResponse(g.Id, g.Name)).ToList(),
            likeCount,
            likedByMe,
            commentCount,
            a.UserId == viewerId);

    public static FeedPostDetailsResponse ToDetails(this FeedPostResponse post, IReadOnlyList<CommentResponse> comments, IReadOnlyList<ElevationSample> elevationProfile) =>
        new(
            post.ActivityId,
            post.Author,
            post.Name,
            post.Caption,
            post.StartedAtUtc,
            post.SharedAtUtc,
            post.DistanceMeters,
            post.ElevationGainMeters,
            post.MovingDurationSeconds,
            post.Privacy,
            post.Track,
            post.Photos,
            post.Groups,
            post.LikeCount,
            post.LikedByMe,
            post.CommentCount,
            post.IsMine,
            comments,
            elevationProfile);

    public static CommentResponse ToResponse(this HikeComment c, string authorName, string viewerId, string hikeAuthorId) =>
        new(c.Id, new FeedUserResponse(c.UserId, authorName), c.Text, c.CreatedAtUtc, c.CanBeDeletedBy(viewerId, hikeAuthorId));
}
