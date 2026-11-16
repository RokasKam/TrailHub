using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Models;

namespace TrailHub.UnitTests;

internal sealed class FakeGroupDirectory : IGroupDirectory
{
    private readonly Dictionary<Guid, (GroupInfo Group, DateTime At)> _groups = [];

    public Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken) =>
        Task.FromResult(_groups.TryGetValue(groupId, out var g) ? g.Group : null);

    public Task<IReadOnlyList<GroupInfo>> GetManyAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GroupInfo>>(groupIds.Where(_groups.ContainsKey).Select(id => _groups[id].Group).ToList());

    public Task<SnapshotResult> ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? previous = null;
        if (_groups.TryGetValue(groupId, out var existing))
        {
            if (existing.At > occurredAtUtc)
            {
                return Task.FromResult(SnapshotResult.Stale);
            }

            previous = existing.Group.Members.Select(m => m.UserId).ToList();
        }

        _groups[groupId] = (new GroupInfo(groupId, name, deleted ? [] : members), occurredAtUtc);
        return Task.FromResult(new SnapshotResult(true, previous));
    }
}
