using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Interfaces.Repository;

public interface IGroupRepository
{
    Task<HikingGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<HikingGroup?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<HikingGroup>> GetForMemberAsync(string userId, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid groupId, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<HikingGroup>> GetAllAsync(CancellationToken cancellationToken);

    void Add(HikingGroup group);

    void Remove(HikingGroup group);
}
