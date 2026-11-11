using TrailHub.Activity.Application.Interfaces.Providers;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Mappings;
using TrailHub.Activity.Application.Models;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Application.Services;

internal sealed class HikeFeedService(
    IActivityRepository activities,
    IHikeLikeRepository likes,
    IHikeCommentRepository comments,
    IContentReportRepository reports,
    IGroupDirectory directory,
    IModerators moderators,
    IHikePhotoStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IHikeFeedService
{
    public async Task<PagedResult<FeedPostResponse>> GetFeedAsync(Guid? groupId, PageRequest page, CancellationToken cancellationToken)
    {
        var myGroups = await directory.GetForMemberAsync(user.UserId, cancellationToken);
        if (groupId is { } only && !myGroups.Any(g => g.Id == only))
        {
            throw ActivityErrors.GroupNotFound.ToException();
        }

        var groupIds = groupId is { } filter ? [filter] : myGroups.Select(g => g.Id).ToList();
        var posts = await activities.GetFeedPageAsync(user.UserId, groupIds, includeOwn: groupId is null, page, cancellationToken);

        var ids = posts.Items.Select(a => a.Id).ToList();
        var likeCounts = await likes.GetCountsAsync(ids, cancellationToken);
        var liked = await likes.GetLikedAsync(ids, user.UserId, cancellationToken);
        var isModerator = moderators.IsModerator(user.UserId);
        var commentCounts = await comments.GetCountsAsync(ids, user.UserId, isModerator, cancellationToken);
        var names = await NamesAsync(myGroups, posts.Items.Select(a => a.UserId), cancellationToken);

        var items = posts.Items
            .Select(a => a.ToFeedPost(
                user.UserId,
                names.For(a.UserId, a.SharedGroupIds),
                myGroups,
                likeCounts.GetValueOrDefault(a.Id),
                liked.Contains(a.Id),
                commentCounts.GetValueOrDefault(a.Id),
                isModerator,
                storage))
            .ToList();
        return new PagedResult<FeedPostResponse>(items, posts.Page, posts.PageSize, posts.TotalCount);
    }

    public async Task<FeedPostDetailsResponse> GetPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var (activity, myGroups) = await GetVisibleAsync(id, cancellationToken);
        var isModerator = moderators.IsModerator(user.UserId);
        var postComments = (await comments.GetForActivityAsync(activity.Id, cancellationToken))
            .Where(c => c.IsVisibleTo(user.UserId, isModerator))
            .ToList();
        var likeCount = (await likes.GetCountsAsync([activity.Id], cancellationToken)).GetValueOrDefault(activity.Id);
        var likedByMe = (await likes.GetLikedAsync([activity.Id], user.UserId, cancellationToken)).Contains(activity.Id);
        var names = await NamesAsync(myGroups, postComments.Select(c => c.UserId).Append(activity.UserId), cancellationToken);

        var post = activity.ToFeedPost(user.UserId, names.For(activity.UserId, activity.SharedGroupIds), myGroups, likeCount, likedByMe, postComments.Count, isModerator, storage);
        var visible = activity.VisibleSegments(user.UserId);
        return post.ToDetails(
            postComments.Select(c => c.ToResponse(names.For(c.UserId, activity.SharedGroupIds), user.UserId, activity.UserId, isModerator)).ToList(),
            visible.Count == 0 ? [] : ElevationProfile.Build(visible));
    }

    public async Task<LikeResponse> LikeAsync(Guid id, CancellationToken cancellationToken)
    {
        var (activity, _) = await GetVisibleAsync(id, cancellationToken);
        if (await likes.GetAsync(activity.Id, user.UserId, cancellationToken) is null)
        {
            likes.Add(HikeLike.Create(activity.Id, user.UserId, clock.GetUtcNow().UtcDateTime));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new LikeResponse(await LikeCountAsync(activity.Id, cancellationToken), true);
    }

    public async Task<LikeResponse> UnlikeAsync(Guid id, CancellationToken cancellationToken)
    {
        var (activity, _) = await GetVisibleAsync(id, cancellationToken);
        if (await likes.GetAsync(activity.Id, user.UserId, cancellationToken) is { } like)
        {
            likes.Remove(like);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new LikeResponse(await LikeCountAsync(activity.Id, cancellationToken), false);
    }

    public async Task<CommentResponse> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var (activity, myGroups) = await GetVisibleAsync(id, cancellationToken);
        var comment = HikeComment.Create(activity.Id, user.UserId, request.Text, clock.GetUtcNow().UtcDateTime).GetValueOrThrow();
        comments.Add(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await NamesAsync(myGroups, [user.UserId], cancellationToken);
        return comment.ToResponse(names.For(user.UserId, activity.SharedGroupIds), user.UserId, activity.UserId, moderators.IsModerator(user.UserId));
    }

    public async Task DeleteCommentAsync(Guid id, Guid commentId, CancellationToken cancellationToken)
    {
        // Moderators can delete any comment, including on hikes they can't see in their feed.
        var isModerator = moderators.IsModerator(user.UserId);
        var activity = isModerator
            ? await activities.GetByIdAsync(id, cancellationToken) ?? throw ActivityErrors.NotFound.ToException()
            : (await GetVisibleAsync(id, cancellationToken)).Activity;
        var comment = await comments.GetByIdAsync(commentId, cancellationToken);
        if (comment is null || comment.ActivityId != activity.Id)
        {
            throw ActivityErrors.CommentNotFound.ToException();
        }

        if (!comment.CanBeDeletedBy(user.UserId, activity.UserId, isModerator))
        {
            throw (comment.IsVisibleTo(user.UserId, isModerator) ? ActivityErrors.NotCommentAuthor : ActivityErrors.CommentNotFound).ToException();
        }

        comments.Remove(comment);
        await ContentRemoval.RemoveReportsAsync(comment.Id, reports, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Task<(HikeActivity Activity, IReadOnlyList<GroupInfo> MyGroups)> GetVisibleAsync(Guid id, CancellationToken cancellationToken) =>
        activities.GetVisibleAsync(directory, id, user.UserId, cancellationToken);

    private async Task<int> LikeCountAsync(Guid activityId, CancellationToken cancellationToken) =>
        (await likes.GetCountsAsync([activityId], cancellationToken)).GetValueOrDefault(activityId);

    private async Task<DisplayNames> NamesAsync(IReadOnlyList<GroupInfo> myGroups, IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var missing = DisplayNames.Missing(myGroups, userIds);
        var others = missing.Count == 0
            ? new Dictionary<string, string>()
            : await directory.GetDisplayNamesAsync(missing, cancellationToken);
        return new DisplayNames(myGroups, others);
    }
}
