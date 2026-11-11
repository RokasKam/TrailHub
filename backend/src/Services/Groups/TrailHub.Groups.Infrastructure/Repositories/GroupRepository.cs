using Microsoft.EntityFrameworkCore;
using TrailHub.Groups.Application.Interfaces.Repository;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Infrastructure.Repositories;

internal sealed class GroupRepository(GroupsDbContext db) : IGroupRepository
{
    public Task<HikingGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<HikingGroup?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) =>
        db.Groups.FirstOrDefaultAsync(g => g.InviteCode == inviteCode, cancellationToken);

    public async Task<IReadOnlyList<HikingGroup>> GetForMemberAsync(string userId, CancellationToken cancellationToken) =>
        await db.Groups.AsNoTracking().Where(g => g.Members.Any(m => m.UserId == userId)).OrderBy(g => g.Name).ToListAsync(cancellationToken);

    public Task<bool> IsMemberAsync(Guid groupId, string userId, CancellationToken cancellationToken) =>
        db.Groups.AnyAsync(g => g.Id == groupId && g.Members.Any(m => m.UserId == userId), cancellationToken);

    public async Task<IReadOnlyList<HikingGroup>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Groups.AsNoTracking().ToListAsync(cancellationToken);

    public void Add(HikingGroup group) => db.Groups.Add(group);

    public void Remove(HikingGroup group) => db.Groups.Remove(group);
}
