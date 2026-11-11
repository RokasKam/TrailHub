using TrailHub.BuildingBlocks.Application;
using TrailHub.Routes.Application.Interfaces.Repository;
using TrailHub.Routes.Application.Interfaces.Service;
using TrailHub.Routes.Application.Models;

namespace TrailHub.Routes.Application.Services;

internal sealed class GroupMembershipService(IGroupDirectory directory, IUnitOfWork unitOfWork) : IGroupMembershipService
{
    public async Task SyncAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        // Access is checked against the current members on every read, so people who leave lose access with no clean-up.
        await directory.ApplySnapshotAsync(groupId, name, members, deleted, occurredAtUtc, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
