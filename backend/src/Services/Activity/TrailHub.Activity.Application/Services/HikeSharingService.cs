using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Mappings;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;

namespace TrailHub.Activity.Application.Services;

internal sealed class HikeSharingService(
    IActivityRepository activities,
    IGroupDirectory directory,
    IUnitOfWork unitOfWork,
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
        activity.Share(request.GroupIds ?? [], myGroups.Select(g => g.Id).ToList(), request.Caption, request.Privacy, clock.GetUtcNow().UtcDateTime)
            .ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return activity.ToSharing()!;
    }

    public async Task UnshareAsync(Guid id, CancellationToken cancellationToken)
    {
        var activity = await activities.GetOwnAsync(id, user.UserId, cancellationToken);
        activity.Unshare();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
