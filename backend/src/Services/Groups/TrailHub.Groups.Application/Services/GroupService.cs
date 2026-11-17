using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Groups.Application.Interfaces.Providers;
using TrailHub.Groups.Application.Interfaces.Repository;
using TrailHub.Groups.Application.Interfaces.Service;
using TrailHub.Groups.Application.Mappings;
using TrailHub.Groups.Application.Requests;
using TrailHub.Groups.Application.Responses;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Services;

internal sealed class GroupService(
    IGroupRepository groups,
    IUnitOfWork unitOfWork,
    ILiveLocationStore locations,
    ILiveLocationNotifier notifier,
    IEventPublisher events,
    ICurrentUser user,
    TimeProvider clock) : IGroupService
{
    public async Task<IReadOnlyList<GroupSummaryResponse>> GetMyGroupsAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var mine = await groups.GetForMemberAsync(user.UserId, cancellationToken);
        return mine
            .Select(g => new GroupSummaryResponse(
                g.Id,
                g.Name,
                g.Members.Count,
                g.Members.First(m => m.UserId == user.UserId).Role,
                g.Hikes.Where(h => h.Status == GroupHikeStatus.Scheduled && h.ScheduledAtUtc > now).Select(h => (DateTime?)h.ScheduledAtUtc).Min()))
            .ToList();
    }

    public async Task<GroupResponse> GetGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken);
        return group is null || !group.IsMember(user.UserId)
            ? throw GroupErrors.NotFound.ToException()
            : group.ToResponse(user.UserId, clock.GetUtcNow().UtcDateTime);
    }

    public async Task<GroupResponse> CreateAsync(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var group = HikingGroup.Create(request.Name, request.Description, user.UserId, MemberName(request.DisplayName), InviteCodes.Generate(), now)
            .GetValueOrThrow();

        groups.Add(group);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishMembersAsync(group, now, cancellationToken);
        return group.ToResponse(user.UserId, now);
    }

    public async Task<GroupResponse> JoinAsync(JoinGroupRequest request, CancellationToken cancellationToken)
    {
        var group = await groups.GetByInviteCodeAsync(request.InviteCode.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw GroupErrors.InvalidInviteCode.ToException();

        var now = clock.GetUtcNow().UtcDateTime;
        group.Join(user.UserId, MemberName(request.DisplayName), request.InviteCode.Trim(), now).ThrowIfFailure();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishMembersAsync(group, now, cancellationToken);
        return group.ToResponse(user.UserId, now);
    }

    public async Task LeaveAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await UpdateAsync(groupId, g => g.Leave(user.UserId), cancellationToken);
        await events.PublishMembersAsync(group, clock.GetUtcNow().UtcDateTime, cancellationToken);
        await locations.RemoveAsync(groupId, user.UserId, cancellationToken);
        await notifier.LocationRemovedAsync(groupId, user.UserId, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid groupId, string userId, CancellationToken cancellationToken)
    {
        var group = await UpdateAsync(groupId, g => g.RemoveMember(user.UserId, userId), cancellationToken);
        await events.PublishMembersAsync(group, clock.GetUtcNow().UtcDateTime, cancellationToken);
        await locations.RemoveAsync(groupId, userId, cancellationToken);
        await notifier.LocationRemovedAsync(groupId, userId, cancellationToken);
    }

    public async Task<GroupResponse> TransferOwnershipAsync(Guid groupId, TransferOwnershipRequest request, CancellationToken cancellationToken)
    {
        var group = await UpdateAsync(groupId, g => g.TransferOwnership(user.UserId, request.UserId), cancellationToken);
        return group.ToResponse(user.UserId, clock.GetUtcNow().UtcDateTime);
    }

    public async Task<GroupResponse> RegenerateInviteCodeAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await UpdateAsync(groupId, g => g.RegenerateInviteCode(user.UserId, InviteCodes.Generate()), cancellationToken);
        return group.ToResponse(user.UserId, clock.GetUtcNow().UtcDateTime);
    }

    public async Task DeleteAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken);
        if (group is null || !group.IsMember(user.UserId))
        {
            throw GroupErrors.NotFound.ToException();
        }

        if (group.OwnerId != user.UserId)
        {
            throw GroupErrors.NotOwner.ToException();
        }

        groups.Remove(group);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishMembersAsync(group, clock.GetUtcNow().UtcDateTime, cancellationToken, deleted: true);
    }

    public async Task RenameMemberAsync(string userId, string displayName, CancellationToken cancellationToken)
    {
        var changed = new List<HikingGroup>();
        foreach (var group in await groups.GetForMemberForUpdateAsync(userId, cancellationToken))
        {
            if (group.RenameMember(userId, displayName))
            {
                changed.Add(group);
            }
        }

        if (changed.Count == 0)
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var group in changed)
        {
            await events.PublishMembersAsync(group, now, cancellationToken);
        }
    }

    public async Task RepublishMembersAsync(CancellationToken cancellationToken)
    {
        var all = await groups.GetAllForUpdateAsync(cancellationToken);

        // One-off cleanup: members who joined with their email as their name get a readable one before it's republished.
        var anyFixed = false;
        foreach (var group in all)
        {
            anyFixed |= group.FixEmailMemberNames();
        }

        if (anyFixed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var group in all)
        {
            await events.PublishMembersAsync(group, now, cancellationToken);
        }
    }

    /// <summary>The name the caller joins with: the one the app sends (their profile name), never an email.</summary>
    private string MemberName(string? requested) => DisplayNames.Friendly(requested ?? user.Name, user.Email);

    /// <summary>Shared shape for member/owner actions on an existing group: load, apply, save.</summary>
    private async Task<HikingGroup> UpdateAsync(Guid groupId, Func<HikingGroup, Result> action, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken) ?? throw GroupErrors.NotFound.ToException();
        action(group).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return group;
    }
}
