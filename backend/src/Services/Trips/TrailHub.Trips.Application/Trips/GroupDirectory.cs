using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trips.Application.Trips;

public sealed record GroupMemberInfo(string UserId, string DisplayName);

public sealed record GroupInfo(Guid Id, string Name, IReadOnlyList<GroupMemberInfo> Members)
{
    public bool IsMember(string userId) => Members.Any(m => m.UserId == userId);

    public string? NameOf(string userId) => Members.FirstOrDefault(m => m.UserId == userId)?.DisplayName;
}

/// <summary>
/// This service's copy of group memberships, kept current by events from the Groups service
/// (services don't share databases). Used to decide who can see a shared trip.
/// </summary>
public interface IGroupDirectory
{
    Task<GroupInfo?> GetAsync(Guid groupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetGroupIdsForMemberAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Stores the snapshot unless a newer one is already stored. Returns whether it was applied.</summary>
    Task<bool> ApplySnapshotAsync(Guid groupId, string name, IReadOnlyList<GroupMemberInfo> members, bool deleted, DateTime occurredAtUtc, CancellationToken cancellationToken);
}

/// <summary>Tells people looking at a trip that it changed, so their app reloads it.</summary>
public interface ITripChangeNotifier
{
    Task TripChangedAsync(Guid tripId, CancellationToken cancellationToken);
}

public sealed record SyncGroupMembersCommand(Guid GroupId, string Name, IReadOnlyList<GroupMemberInfo> Members, bool Deleted, DateTime OccurredAtUtc) : ICommand;

/// <summary>Updates the membership copy, then fixes up trips shared with the group (claims of people who left, deleted groups).</summary>
internal sealed class SyncGroupMembersHandler(IGroupDirectory directory, ITripRepository trips, IUnitOfWork unitOfWork, ITripChangeNotifier notifier)
    : ICommandHandler<SyncGroupMembersCommand>
{
    public async Task<Result> Handle(SyncGroupMembersCommand command, CancellationToken cancellationToken)
    {
        if (!await directory.ApplySnapshotAsync(command.GroupId, command.Name, command.Members, command.Deleted, command.OccurredAtUtc, cancellationToken))
        {
            return Result.Success(); // stale, a newer snapshot was already applied
        }

        HashSet<string> memberIds = command.Deleted ? [] : command.Members.Select(m => m.UserId).ToHashSet();
        var changed = new List<Guid>();
        foreach (var trip in await trips.GetByGroupAsync(command.GroupId, cancellationToken))
        {
            if (command.Deleted ? trip.UnlinkGroup().IsSuccess : trip.SyncGroupMembers(memberIds))
            {
                changed.Add(trip.Id);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var tripId in changed)
        {
            await notifier.TripChangedAsync(tripId, cancellationToken);
        }

        return Result.Success();
    }
}
