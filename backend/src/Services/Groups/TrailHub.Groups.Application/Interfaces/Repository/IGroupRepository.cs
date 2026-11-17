using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Interfaces.Repository;

public interface IGroupRepository
{
    Task<HikingGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<HikingGroup?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<HikingGroup>> GetForMemberAsync(string userId, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid groupId, string userId, CancellationToken cancellationToken);

    /// <summary>The user's groups, tracked so changes to them are saved.</summary>
    Task<IReadOnlyList<HikingGroup>> GetForMemberForUpdateAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Every group, tracked so changes to them are saved.</summary>
    Task<IReadOnlyList<HikingGroup>> GetAllForUpdateAsync(CancellationToken cancellationToken);

    void Add(HikingGroup group);

    void Remove(HikingGroup group);
}
