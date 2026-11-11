using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Activity.Application.Interfaces.Service;

/// <summary>Hikes shared with the caller's groups, with likes and comments.</summary>
public interface IHikeFeedService
{
    /// <summary>Posts shared with the caller's groups (or one of them) plus their own, newest share first.</summary>
    Task<PagedResult<FeedPostResponse>> GetFeedAsync(Guid? groupId, PageRequest page, CancellationToken cancellationToken);

    Task<FeedPostDetailsResponse> GetPostAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Idempotent.</summary>
    Task<LikeResponse> LikeAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Idempotent.</summary>
    Task<LikeResponse> UnlikeAsync(Guid id, CancellationToken cancellationToken);

    Task<CommentResponse> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken cancellationToken);

    /// <summary>Allowed for the comment's author and the hike's author.</summary>
    Task DeleteCommentAsync(Guid id, Guid commentId, CancellationToken cancellationToken);
}
