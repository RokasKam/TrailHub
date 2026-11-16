using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Application.Requests;
using TrailHub.Notifications.Domain.Groups;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Application.Services;

internal sealed class GroupMembershipService(IGroupDirectory directory, NotificationDispatcher dispatcher) : IGroupMembershipService
{
    public async Task SyncGroupMembersAsync(SyncGroupMembersRequest request, CancellationToken cancellationToken)
    {
        var result = await directory.ApplySnapshotAsync(request.GroupId, request.Name, request.Members, request.Deleted, request.OccurredAtUtc, cancellationToken);
        if (!result.Applied)
        {
            return; // stale: a newer snapshot was already applied
        }

        var current = request.Deleted ? [] : request.Members.Select(m => m.UserId).Distinct().ToList();
        var joined = MembershipDiff.Joined(result.PreviousMemberIds, current);
        var stayed = current.Where(id => result.PreviousMemberIds?.Contains(id) == true).ToList();

        // The snapshot and the notifications are saved together, so a redelivered snapshot is stale and adds nothing.
        var added = new List<Notification>();
        foreach (var joinerId in joined)
        {
            var name = request.Members.First(m => m.UserId == joinerId).DisplayName;
            foreach (var memberId in stayed)
            {
                if (await dispatcher.AddAsync(
                        memberId,
                        joinerId,
                        NotificationKind.GroupMemberJoined,
                        $"New member in {request.Name}",
                        $"{name} joined {request.Name}",
                        $"/groups/{request.GroupId}",
                        null,
                        cancellationToken) is { } notification)
                {
                    added.Add(notification);
                }
            }
        }

        await dispatcher.SaveAndPushAsync(added, cancellationToken);
    }
}
