using TrailHub.BuildingBlocks.Application;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Requests;

namespace TrailHub.Trips.Application.Services;

internal sealed class GroupMembershipService(IGroupDirectory directory, ITripRepository trips, IUnitOfWork unitOfWork, ITripChangeNotifier notifier)
    : IGroupMembershipService
{
    public async Task SyncGroupMembersAsync(SyncGroupMembersRequest request, CancellationToken cancellationToken)
    {
        if (!await directory.ApplySnapshotAsync(request.GroupId, request.Name, request.Members, request.Deleted, request.OccurredAtUtc, cancellationToken))
        {
            return; // stale, a newer snapshot was already applied
        }

        HashSet<string> memberIds = request.Deleted ? [] : request.Members.Select(m => m.UserId).ToHashSet();
        var changed = new List<Guid>();
        foreach (var trip in await trips.GetByGroupAsync(request.GroupId, cancellationToken))
        {
            if (request.Deleted ? trip.UnlinkGroup().IsSuccess : trip.SyncGroupMembers(memberIds))
            {
                changed.Add(trip.Id);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var tripId in changed)
        {
            await notifier.TripChangedAsync(tripId, cancellationToken);
        }
    }
}
