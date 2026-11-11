using Microsoft.EntityFrameworkCore;
using TrailHub.Routes.Application.Interfaces.Repository;
using TrailHub.Routes.Application.Models;

namespace TrailHub.Routes.Infrastructure.Repositories;

internal sealed class GroupDirectory(RoutesDbContext db) : IGroupDirectory
{
    public async Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken) =>
        ToInfo(await db.KnownGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId && !g.Deleted, cancellationToken));

    public async Task<IReadOnlyList<GroupInfo>> GetForMemberAsync(string userId, CancellationToken cancellationToken) =>
        (await db.KnownGroups.AsNoTracking().Where(g => !g.Deleted && g.Members.Any(m => m.UserId == userId)).ToListAsync(cancellationToken))
        .Select(g => ToInfo(g)!)
        .ToList();

    public async Task ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        var group = await db.KnownGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            group = new KnownGroup { Id = groupId };
            db.KnownGroups.Add(group);
        }
        else if (group.UpdatedAtUtc > occurredAtUtc)
        {
            return;
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
    }

    private static GroupInfo? ToInfo(KnownGroup? group) =>
        group is null ? null : new GroupInfo(group.Id, group.Name, group.Members.Select(m => new GroupMemberInfo(m.UserId, m.DisplayName)).ToList());
}
