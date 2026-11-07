using MassTransit;
using Microsoft.EntityFrameworkCore;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Contracts;
using TrailHub.Routes.Application.Routes;

namespace TrailHub.Routes.Infrastructure;

/// <summary>A group as last reported by the Groups service. Deleted groups stay as a tombstone so late, older snapshots are ignored.</summary>
internal sealed class KnownGroup
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public bool Deleted { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<KnownGroupMember> Members { get; set; } = [];
}

internal sealed class KnownGroupMember
{
    public string UserId { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
}

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

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class GroupMembersChangedConsumer(ICommandHandler<SyncGroupMembersCommand> handler) : IConsumer<GroupMembersChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<GroupMembersChangedIntegrationEvent> context)
    {
        var e = context.Message;
        return handler.Handle(
            new SyncGroupMembersCommand(e.GroupId, e.Name, e.Members.Select(m => new GroupMemberInfo(m.UserId, m.DisplayName)).ToList(), e.Deleted, e.OccurredAtUtc),
            context.CancellationToken);
    }
}
