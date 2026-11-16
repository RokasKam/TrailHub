using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Mappings;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Contracts;

namespace TrailHub.Activity.Application.Services;

internal sealed class HikeSharingService(
    IActivityRepository activities,
    IGroupDirectory directory,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    ICurrentUser user,
    TimeProvider clock) : IHikeSharingService
{
    public async Task<IReadOnlyList<ShareTargetResponse>> GetShareTargetsAsync(CancellationToken cancellationToken) =>
        (await directory.GetForMemberAsync(user.UserId, cancellationToken))
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ShareTargetResponse(g.Id, g.Name, g.Members.Count))
            .ToList();

    public async Task<HikeSharingResponse> ShareAsync(Guid id, ShareHikeRequest request, CancellationToken cancellationToken)
    {
        var activity = await activities.GetOwnAsync(id, user.UserId, cancellationToken);
        var myGroups = await directory.GetForMemberAsync(user.UserId, cancellationToken);
        var before = activity.IsShared ? activity.SharedGroupIds.ToHashSet() : [];
        activity.Share(request.GroupIds ?? [], myGroups.Select(g => g.Id).ToList(), request.Caption, request.Privacy, clock.GetUtcNow().UtcDateTime)
            .ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Announce the first share, and later only the groups that were added (so members aren't told twice).
        var added = activity.SharedGroupIds.Where(g => !before.Contains(g)).ToList();
        if (added.Count > 0)
        {
            var authorName = new DisplayNames(myGroups, new Dictionary<string, string>()).For(user.UserId, activity.SharedGroupIds);
            await events.PublishAsync(new HikeSharedIntegrationEvent(activity.Id, activity.UserId, authorName, activity.Name, added), cancellationToken);
        }

        return activity.ToSharing()!;
    }

    public async Task UnshareAsync(Guid id, CancellationToken cancellationToken)
    {
        var activity = await activities.GetOwnAsync(id, user.UserId, cancellationToken);
        activity.Unshare();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
