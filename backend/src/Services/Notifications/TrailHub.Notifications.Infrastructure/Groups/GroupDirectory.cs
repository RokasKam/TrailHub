using Microsoft.EntityFrameworkCore;
using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Models;
using TrailHub.Notifications.Infrastructure.Persistence;

namespace TrailHub.Notifications.Infrastructure.Groups;

internal sealed class GroupDirectory(NotificationsDbContext db) : IGroupDirectory
{
    public async Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await db.KnownGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId && !g.Deleted, cancellationToken);
        return group is null ? null : ToInfo(group);
    }

    public async Task<IReadOnlyList<GroupInfo>> GetManyAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        var ids = groupIds.ToArray();
        var groups = await db.KnownGroups.AsNoTracking().Where(g => ids.Contains(g.Id) && !g.Deleted).ToListAsync(cancellationToken);

        // In the order asked for, so "the first group" is stable.
        return ids.Select(id => groups.Find(g => g.Id == id)).OfType<KnownGroup>().Select(ToInfo).ToList();
    }

    public async Task<SnapshotResult> ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        var group = await db.KnownGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        IReadOnlyList<string>? previous = null;
        if (group is null)
        {
            group = new KnownGroup { Id = groupId };
            db.KnownGroups.Add(group);
        }
        else if (group.UpdatedAtUtc > occurredAtUtc)
        {
            return SnapshotResult.Stale;
        }
        else
        {
            previous = group.Members.Select(m => m.UserId).ToList();
        }

        group.Name = name;
        group.Deleted = deleted;
        group.UpdatedAtUtc = occurredAtUtc;

        // Diff rather than replace: re-adding an owned entity with the same key in one context confuses EF's tracking.
        Dictionary<string, GroupMemberInfo> incoming = deleted ? [] : members.DistinctBy(m => m.UserId).ToDictionary(m => m.UserId);
        group.Members.RemoveAll(m => !incoming.ContainsKey(m.UserId));
        foreach (var member in incoming.Values)
        {
            if (group.Members.Find(m => m.UserId == member.UserId) is { } existing)
            {
                existing.DisplayName = member.DisplayName;
            }
            else
            {
                group.Members.Add(new KnownGroupMember { UserId = member.UserId, DisplayName = member.DisplayName });
            }
        }

        return new SnapshotResult(true, previous);
    }

    private static GroupInfo ToInfo(KnownGroup group) =>
        new(group.Id, group.Name, group.Members.Select(m => new GroupMemberInfo(m.UserId, m.DisplayName)).ToList());
}
