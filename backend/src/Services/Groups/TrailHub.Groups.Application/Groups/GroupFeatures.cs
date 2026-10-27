using System.Security.Cryptography;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Contracts;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Groups;

public interface IGroupRepository
{
    Task<HikingGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<HikingGroup?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<HikingGroup>> GetForMemberAsync(string userId, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid groupId, string userId, CancellationToken cancellationToken);

    void Add(HikingGroup group);

    void Remove(HikingGroup group);
}

public sealed record GroupMemberDto(string UserId, string DisplayName, GroupRole Role, DateTime JoinedAtUtc);

public sealed record RsvpDto(string UserId, RsvpStatus Status, DateTime RespondedAtUtc);

public sealed record GroupHikeDto(
    Guid Id,
    string Title,
    Guid? TrailId,
    DateTime ScheduledAtUtc,
    double? MeetingLatitude,
    double? MeetingLongitude,
    string? Notes,
    string CreatedBy,
    GroupHikeStatus Status,
    IReadOnlyList<RsvpDto> Rsvps);

/// <param name="InviteCode">Only returned to the owner.</param>
public sealed record GroupDto(Guid Id, string Name, string? Description, string? InviteCode, IReadOnlyList<GroupMemberDto> Members, IReadOnlyList<GroupHikeDto> UpcomingHikes, DateTime CreatedAtUtc);

public sealed record GroupSummaryDto(Guid Id, string Name, int MemberCount, GroupRole MyRole, DateTime? NextHikeAtUtc);

internal static class GroupMappings
{
    public static GroupDto ToDto(this HikingGroup g, string viewerId, DateTime nowUtc) => new(
        g.Id,
        g.Name,
        g.Description,
        g.OwnerId == viewerId ? g.InviteCode : null,
        g.Members.Select(m => new GroupMemberDto(m.UserId, m.DisplayName, m.Role, m.JoinedAtUtc)).ToList(),
        g.Hikes
            .Where(h => h.ScheduledAtUtc > nowUtc && h.Status == GroupHikeStatus.Scheduled)
            .OrderBy(h => h.ScheduledAtUtc)
            .Select(h => h.ToDto())
            .ToList(),
        g.CreatedAtUtc);

    public static GroupHikeDto ToDto(this GroupHike h) =>
        new(
            h.Id,
            h.Title,
            h.TrailId,
            h.ScheduledAtUtc,
            h.MeetingPoint?.Latitude,
            h.MeetingPoint?.Longitude,
            h.Notes,
            h.CreatedBy,
            h.Status,
            h.Rsvps.Select(r => new RsvpDto(r.UserId, r.Status, r.RespondedAtUtc)).ToList());
}

internal static class InviteCodes
{
    // No 0/O/1/I: codes get read aloud and typed from screenshots.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, HikingGroup.InviteCodeLength);
}

public sealed record CreateGroupCommand(string Name, string? Description, string? DisplayName) : ICommand<GroupDto>;

internal sealed class CreateGroupHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CreateGroupCommand, GroupDto>
{
    public async Task<Result<GroupDto>> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var group = HikingGroup.Create(command.Name, command.Description, user.UserId, command.DisplayName ?? user.Name ?? "Hiker", InviteCodes.Generate(), now);
        if (group.IsFailure)
        {
            return group.Error!;
        }

        groups.Add(group.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return group.Value.ToDto(user.UserId, now);
    }
}

public sealed record JoinGroupCommand(string InviteCode, string? DisplayName) : ICommand<GroupDto>;

internal sealed class JoinGroupHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<JoinGroupCommand, GroupDto>
{
    public async Task<Result<GroupDto>> Handle(JoinGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await groups.GetByInviteCodeAsync(command.InviteCode.Trim().ToUpperInvariant(), cancellationToken);
        if (group is null)
        {
            return GroupErrors.InvalidInviteCode;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var joined = group.Join(user.UserId, command.DisplayName ?? user.Name ?? "Hiker", command.InviteCode.Trim(), now);
        if (joined.IsFailure)
        {
            return joined.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return group.ToDto(user.UserId, now);
    }
}

/// <summary>Shared shape for member/owner actions on an existing group.</summary>
internal abstract class GroupActionHandler(IGroupRepository groups, IUnitOfWork unitOfWork)
{
    protected async Task<Result<HikingGroup>> RunAsync(Guid groupId, Func<HikingGroup, Result> action, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            return GroupErrors.NotFound;
        }

        var result = action(group);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return group;
    }
}

public sealed record LeaveGroupCommand(Guid GroupId) : ICommand;

internal sealed class LeaveGroupHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ILiveLocationStore locations, ILiveLocationNotifier notifier, ICurrentUser user)
    : GroupActionHandler(groups, unitOfWork), ICommandHandler<LeaveGroupCommand>
{
    public async Task<Result> Handle(LeaveGroupCommand command, CancellationToken cancellationToken)
    {
        var result = await RunAsync(command.GroupId, g => g.Leave(user.UserId), cancellationToken);
        if (result.IsSuccess)
        {
            await locations.RemoveAsync(command.GroupId, user.UserId, cancellationToken);
            await notifier.LocationRemovedAsync(command.GroupId, user.UserId, cancellationToken);
        }

        return result.IsSuccess ? Result.Success() : result.Error!;
    }
}

public sealed record RemoveMemberCommand(Guid GroupId, string UserId) : ICommand;

internal sealed class RemoveMemberHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ILiveLocationStore locations, ILiveLocationNotifier notifier, ICurrentUser user)
    : GroupActionHandler(groups, unitOfWork), ICommandHandler<RemoveMemberCommand>
{
    public async Task<Result> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var result = await RunAsync(command.GroupId, g => g.RemoveMember(user.UserId, command.UserId), cancellationToken);
        if (result.IsSuccess)
        {
            await locations.RemoveAsync(command.GroupId, command.UserId, cancellationToken);
            await notifier.LocationRemovedAsync(command.GroupId, command.UserId, cancellationToken);
        }

        return result.IsSuccess ? Result.Success() : result.Error!;
    }
}

public sealed record TransferOwnershipCommand(Guid GroupId, string NewOwnerId) : ICommand<GroupDto>;

internal sealed class TransferOwnershipHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : GroupActionHandler(groups, unitOfWork), ICommandHandler<TransferOwnershipCommand, GroupDto>
{
    public async Task<Result<GroupDto>> Handle(TransferOwnershipCommand command, CancellationToken cancellationToken)
    {
        var result = await RunAsync(command.GroupId, g => g.TransferOwnership(user.UserId, command.NewOwnerId), cancellationToken);
        return result.IsSuccess ? result.Value.ToDto(user.UserId, clock.GetUtcNow().UtcDateTime) : result.Error!;
    }
}

public sealed record RegenerateInviteCodeCommand(Guid GroupId) : ICommand<GroupDto>;

internal sealed class RegenerateInviteCodeHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : GroupActionHandler(groups, unitOfWork), ICommandHandler<RegenerateInviteCodeCommand, GroupDto>
{
    public async Task<Result<GroupDto>> Handle(RegenerateInviteCodeCommand command, CancellationToken cancellationToken)
    {
        var result = await RunAsync(command.GroupId, g => g.RegenerateInviteCode(user.UserId, InviteCodes.Generate()), cancellationToken);
        return result.IsSuccess ? result.Value.ToDto(user.UserId, clock.GetUtcNow().UtcDateTime) : result.Error!;
    }
}

public sealed record DeleteGroupCommand(Guid GroupId) : ICommand;

internal sealed class DeleteGroupHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ICurrentUser user) : ICommandHandler<DeleteGroupCommand>
{
    public async Task<Result> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null || !group.IsMember(user.UserId))
        {
            return GroupErrors.NotFound;
        }

        if (group.OwnerId != user.UserId)
        {
            return GroupErrors.NotOwner;
        }

        groups.Remove(group);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ScheduleGroupHikeCommand(
    Guid GroupId,
    string Title,
    Guid? TrailId,
    DateTime ScheduledAtUtc,
    double? MeetingLatitude,
    double? MeetingLongitude,
    string? Notes) : ICommand<GroupHikeDto>;

internal sealed class ScheduleGroupHikeHandler(IGroupRepository groups, IUnitOfWork unitOfWork, IEventPublisher events, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ScheduleGroupHikeCommand, GroupHikeDto>
{
    public async Task<Result<GroupHikeDto>> Handle(ScheduleGroupHikeCommand command, CancellationToken cancellationToken)
    {
        GeoPoint? meetingPoint = null;
        if (command.MeetingLatitude is { } lat && command.MeetingLongitude is { } lon)
        {
            var point = GeoPoint.Create(lat, lon);
            if (point.IsFailure)
            {
                return point.Error!;
            }

            meetingPoint = point.Value;
        }

        var group = await groups.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return GroupErrors.NotFound;
        }

        var scheduledAt = DateTime.SpecifyKind(command.ScheduledAtUtc, DateTimeKind.Utc);
        var hike = group.ScheduleHike(user.UserId, command.Title, command.TrailId, scheduledAt, meetingPoint, command.Notes, clock.GetUtcNow().UtcDateTime);
        if (hike.IsFailure)
        {
            return hike.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishAsync(
            new GroupHikeScheduledIntegrationEvent(group.Id, hike.Value.Id, hike.Value.Title, hike.Value.ScheduledAtUtc, group.Members.Select(m => m.UserId).ToList()),
            cancellationToken);
        return hike.Value.ToDto();
    }
}

public sealed record CancelGroupHikeCommand(Guid GroupId, Guid HikeId) : ICommand;

internal sealed class CancelGroupHikeHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ICurrentUser user)
    : GroupActionHandler(groups, unitOfWork), ICommandHandler<CancelGroupHikeCommand>
{
    public async Task<Result> Handle(CancelGroupHikeCommand command, CancellationToken cancellationToken)
    {
        var result = await RunAsync(command.GroupId, g => g.CancelHike(user.UserId, command.HikeId), cancellationToken);
        return result.IsSuccess ? Result.Success() : result.Error!;
    }
}

public sealed record RsvpGroupHikeCommand(Guid GroupId, Guid HikeId, RsvpStatus Status) : ICommand<GroupHikeDto>;

internal sealed class RsvpGroupHikeHandler(IGroupRepository groups, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<RsvpGroupHikeCommand, GroupHikeDto>
{
    public async Task<Result<GroupHikeDto>> Handle(RsvpGroupHikeCommand command, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null || !group.IsMember(user.UserId))
        {
            return GroupErrors.NotFound;
        }

        var result = group.Rsvp(user.UserId, command.HikeId, command.Status, clock.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return group.Hikes.First(h => h.Id == command.HikeId).ToDto();
    }
}

public sealed record GetMyGroupsQuery : IQuery<IReadOnlyList<GroupSummaryDto>>;

internal sealed class GetMyGroupsHandler(IGroupRepository groups, ICurrentUser user, TimeProvider clock) : IQueryHandler<GetMyGroupsQuery, IReadOnlyList<GroupSummaryDto>>
{
    public async Task<Result<IReadOnlyList<GroupSummaryDto>>> Handle(GetMyGroupsQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var mine = await groups.GetForMemberAsync(user.UserId, cancellationToken);
        return mine
            .Select(g => new GroupSummaryDto(
                g.Id,
                g.Name,
                g.Members.Count,
                g.Members.First(m => m.UserId == user.UserId).Role,
                g.Hikes.Where(h => h.Status == GroupHikeStatus.Scheduled && h.ScheduledAtUtc > now).Select(h => (DateTime?)h.ScheduledAtUtc).Min()))
            .ToList();
    }
}

public sealed record GetGroupQuery(Guid GroupId) : IQuery<GroupDto>;

internal sealed class GetGroupHandler(IGroupRepository groups, ICurrentUser user, TimeProvider clock) : IQueryHandler<GetGroupQuery, GroupDto>
{
    public async Task<Result<GroupDto>> Handle(GetGroupQuery query, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(query.GroupId, cancellationToken);
        return group is null || !group.IsMember(user.UserId) ? GroupErrors.NotFound : group.ToDto(user.UserId, clock.GetUtcNow().UtcDateTime);
    }
}
