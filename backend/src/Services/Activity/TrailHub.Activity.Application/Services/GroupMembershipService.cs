using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Models;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Activity.Application.Services;

internal sealed class GroupMembershipService(IGroupDirectory directory, IUnitOfWork unitOfWork) : IGroupMembershipService
{
    public async Task SyncAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        // Feed access is checked against the current members on every read, so people who leave stop seeing the
        // group's posts with no clean-up.
        await directory.ApplySnapshotAsync(groupId, name, members, deleted, occurredAtUtc, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
