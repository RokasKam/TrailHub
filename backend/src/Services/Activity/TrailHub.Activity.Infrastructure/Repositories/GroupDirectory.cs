using Microsoft.EntityFrameworkCore;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Models;
using TrailHub.Activity.Infrastructure.Persistence;

namespace TrailHub.Activity.Infrastructure.Repositories;

internal sealed class GroupDirectory(ActivityDbContext db) : IGroupDirectory
{
    public async Task<IReadOnlyList<GroupInfo>> GetForMemberAsync(string userId, CancellationToken cancellationToken) =>
        (await db.KnownGroups.AsNoTracking().Where(g => !g.Deleted && g.Members.Any(m => m.UserId == userId)).ToListAsync(cancellationToken))
        .Select(ToInfo)
        .ToList();

    public async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        var rows = await db.KnownGroups.AsNoTracking()
            .Where(g => !g.Deleted)
            .SelectMany(g => g.Members.Where(m => ids.Contains(m.UserId)).Select(m => new { m.UserId, m.DisplayName, g.UpdatedAtUtc }))
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.UpdatedAtUtc).First().DisplayName);
    }

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
            return; // stale: a newer snapshot was already applied
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

    private static GroupInfo ToInfo(KnownGroup group) =>
        new(group.Id, group.Name, group.Members.Select(m => new GroupMemberInfo(m.UserId, m.DisplayName)).ToList());
}
