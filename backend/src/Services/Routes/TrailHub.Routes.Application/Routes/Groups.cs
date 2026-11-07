using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Routes.Application.Routes;

public sealed record GroupMemberInfo(string UserId, string DisplayName);

public sealed record GroupInfo(Guid Id, string Name, IReadOnlyList<GroupMemberInfo> Members)
{
    public bool IsMember(string userId) => Members.Any(m => m.UserId == userId);
}

/// <summary>
/// This service's copy of group memberships, kept current by events from the Groups service, to decide who can
/// see a route shared with a group. (Services don't share databases; Trips keeps its own copy the same way.)
/// </summary>
public interface IGroupDirectory
{
    Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GroupInfo>> GetForMemberAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Stores the snapshot unless a newer one is already stored.</summary>
    Task ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken);
}

public sealed record SyncGroupMembersCommand(Guid GroupId, string Name, IReadOnlyList<GroupMemberInfo> Members, bool Deleted, DateTime OccurredAtUtc) : ICommand;

internal sealed class SyncGroupMembersHandler(IGroupDirectory directory, IUnitOfWork unitOfWork) : ICommandHandler<SyncGroupMembersCommand>
{
    public async Task<Result> Handle(SyncGroupMembersCommand command, CancellationToken cancellationToken)
    {
        // Access is checked against the current members on every read, so people who leave lose access with no clean-up.
        await directory.ApplySnapshotAsync(command.GroupId, command.Name, command.Members, command.Deleted, command.OccurredAtUtc, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
