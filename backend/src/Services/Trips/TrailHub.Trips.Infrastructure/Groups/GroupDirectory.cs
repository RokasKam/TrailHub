using Microsoft.EntityFrameworkCore;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Models;
using TrailHub.Trips.Infrastructure.Persistence;

namespace TrailHub.Trips.Infrastructure.Groups;

internal sealed class GroupDirectory(TripsDbContext db) : IGroupDirectory
{
    public async Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await db.KnownGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId && !g.Deleted, cancellationToken);
        return group is null ? null : new GroupInfo(group.Id, group.Name, group.Members.Select(m => new GroupMemberInfo(m.UserId, m.DisplayName)).ToList());
    }

    public async Task<IReadOnlyList<Guid>> GetGroupIdsForMemberAsync(string userId, CancellationToken cancellationToken) =>
        await db.KnownGroups.Where(g => !g.Deleted && g.Members.Any(m => m.UserId == userId)).Select(g => g.Id).ToListAsync(cancellationToken);

    public async Task<bool> ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        var group = await db.KnownGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            group = new KnownGroup { Id = groupId };
            db.KnownGroups.Add(group);
        }
        else if (group.UpdatedAtUtc > occurredAtUtc)
        {
            return false;
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

        return true;
    }
}
