using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Groups.Domain.Groups;

public enum GroupRole
{
    Owner,
    Member,
}

public enum GroupHikeStatus
{
    Scheduled,
    Cancelled,
}

public enum RsvpStatus
{
    Going,
    Maybe,
    NotGoing,
}

public sealed record HikeRsvp(string UserId, RsvpStatus Status, DateTime RespondedAtUtc);

public sealed class GroupMember
{
    private GroupMember()
    {
    }

    internal GroupMember(string userId, string displayName, GroupRole role, DateTime joinedAtUtc)
    {
        UserId = userId;
        DisplayName = displayName;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public string UserId { get; private set; } = null!;

    /// <summary>Snapshot of the member's profile name at join time; the Profile service owns the real one.</summary>
    public string DisplayName { get; private set; } = null!;

    public GroupRole Role { get; internal set; }

    public DateTime JoinedAtUtc { get; private set; }
}

public sealed class GroupHike : Entity<Guid>
{
    private readonly List<HikeRsvp> _rsvps = [];

    private GroupHike()
    {
    }

    internal GroupHike(string title, Guid? trailId, DateTime scheduledAtUtc, GeoPoint? meetingPoint, string? notes, string createdBy)
    {
        Id = Guid.CreateVersion7();
        Title = title;
        TrailId = trailId;
        ScheduledAtUtc = scheduledAtUtc;
        MeetingPoint = meetingPoint;
        Notes = notes;
        CreatedBy = createdBy;
        Status = GroupHikeStatus.Scheduled;
    }

    public string Title { get; private set; } = null!;

    public Guid? TrailId { get; private set; }

    public DateTime ScheduledAtUtc { get; private set; }

    public GeoPoint? MeetingPoint { get; private set; }

    public string? Notes { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public GroupHikeStatus Status { get; internal set; }

    public IReadOnlyList<HikeRsvp> Rsvps => _rsvps;

    internal void SetRsvp(string userId, RsvpStatus status, DateTime nowUtc)
    {
        _rsvps.RemoveAll(r => r.UserId == userId);
        _rsvps.Add(new HikeRsvp(userId, status, nowUtc));
    }

    internal void RemoveRsvp(string userId) => _rsvps.RemoveAll(r => r.UserId == userId);
}

/// <summary>
/// A set of hikers who plan hikes together and can see each other's live location.
/// People join with an invite code shared out-of-band (chat, QR), so the service never needs to look up other users.
/// </summary>
public sealed class HikingGroup : AggregateRoot<Guid>
{
    public const int MaxNameLength = 100;
    public const int MaxTextLength = 2000;
    public const int MaxMembers = 50;
    public const int InviteCodeLength = 8;

    private readonly List<GroupMember> _members = [];
    private readonly List<GroupHike> _hikes = [];

    private HikingGroup()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public string InviteCode { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<GroupMember> Members => _members;

    public IReadOnlyList<GroupHike> Hikes => _hikes;

    public string OwnerId => _members.First(m => m.Role == GroupRole.Owner).UserId;

    public static Result<HikingGroup> Create(string name, string? description, string ownerId, string ownerDisplayName, string inviteCode, DateTime nowUtc)
    {
        var error = ValidateDetails(name, description) ?? ValidateInviteCode(inviteCode);
        if (error is not null)
        {
            return error;
        }

        var group = new HikingGroup
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            InviteCode = inviteCode,
            CreatedAtUtc = nowUtc,
        };
        group._members.Add(new GroupMember(ownerId, ownerDisplayName, GroupRole.Owner, nowUtc));
        return group;
    }

    public bool IsMember(string userId) => _members.Exists(m => m.UserId == userId);

    public Result Join(string userId, string displayName, string inviteCode, DateTime nowUtc)
    {
        if (!string.Equals(inviteCode, InviteCode, StringComparison.OrdinalIgnoreCase))
        {
            return GroupErrors.InvalidInviteCode;
        }

        if (IsMember(userId))
        {
            return GroupErrors.AlreadyMember;
        }

        if (_members.Count >= MaxMembers)
        {
            return GroupErrors.GroupFull;
        }

        _members.Add(new GroupMember(userId, displayName, GroupRole.Member, nowUtc));
        return Result.Success();
    }

    /// <summary>The owner can only leave by handing the group over first (or deleting it).</summary>
    public Result Leave(string userId)
    {
        if (_members.Find(m => m.UserId == userId) is not { } member)
        {
            return GroupErrors.NotMember;
        }

        if (member.Role == GroupRole.Owner)
        {
            return GroupErrors.OwnerCannotLeave;
        }

        _members.Remove(member);
        _hikes.ForEach(h => h.RemoveRsvp(userId));
        return Result.Success();
    }

    public Result RemoveMember(string actorId, string userId)
    {
        if (actorId != OwnerId)
        {
            return GroupErrors.NotOwner;
        }

        return actorId == userId ? GroupErrors.OwnerCannotLeave : Leave(userId);
    }

    public Result TransferOwnership(string actorId, string newOwnerId)
    {
        if (actorId != OwnerId)
        {
            return GroupErrors.NotOwner;
        }

        if (_members.Find(m => m.UserId == newOwnerId) is not { } newOwner)
        {
            return GroupErrors.NotMember;
        }

        _members.First(m => m.UserId == actorId).Role = GroupRole.Member;
        newOwner.Role = GroupRole.Owner;
        return Result.Success();
    }

    public Result RegenerateInviteCode(string actorId, string newCode)
    {
        if (actorId != OwnerId)
        {
            return GroupErrors.NotOwner;
        }

        if (ValidateInviteCode(newCode) is { } error)
        {
            return error;
        }

        InviteCode = newCode;
        return Result.Success();
    }

    public Result<GroupHike> ScheduleHike(string actorId, string title, Guid? trailId, DateTime scheduledAtUtc, GeoPoint? meetingPoint, string? notes, DateTime nowUtc)
    {
        if (!IsMember(actorId))
        {
            return GroupErrors.NotMember;
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > MaxNameLength || notes?.Length > MaxTextLength)
        {
            return GroupErrors.InvalidHike;
        }

        if (scheduledAtUtc <= nowUtc)
        {
            return GroupErrors.HikeInPast;
        }

        var hike = new GroupHike(title.Trim(), trailId, scheduledAtUtc, meetingPoint, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), actorId);
        hike.SetRsvp(actorId, RsvpStatus.Going, nowUtc); // the organizer is going
        _hikes.Add(hike);
        return hike;
    }

    /// <summary>The organizer or the group owner can cancel.</summary>
    public Result CancelHike(string actorId, Guid hikeId)
    {
        if (_hikes.Find(h => h.Id == hikeId) is not { } hike)
        {
            return GroupErrors.HikeNotFound;
        }

        if (actorId != hike.CreatedBy && actorId != OwnerId)
        {
            return GroupErrors.NotOwner;
        }

        hike.Status = GroupHikeStatus.Cancelled;
        return Result.Success();
    }

    /// <summary>A member's answer to a scheduled hike. Answering again replaces the previous answer.</summary>
    public Result Rsvp(string actorId, Guid hikeId, RsvpStatus status, DateTime nowUtc)
    {
        if (!IsMember(actorId))
        {
            return GroupErrors.NotMember;
        }

        if (_hikes.Find(h => h.Id == hikeId) is not { } hike)
        {
            return GroupErrors.HikeNotFound;
        }

        if (hike.Status == GroupHikeStatus.Cancelled || hike.ScheduledAtUtc <= nowUtc)
        {
            return GroupErrors.HikeClosed;
        }

        hike.SetRsvp(actorId, status, nowUtc);
        return Result.Success();
    }

    private static Error? ValidateDetails(string name, string? description) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength ? GroupErrors.InvalidName
        : description?.Length > MaxTextLength ? GroupErrors.TextTooLong
        : null;

    private static Error? ValidateInviteCode(string code) =>
        code.Length == InviteCodeLength && code.All(char.IsLetterOrDigit) ? null : GroupErrors.InvalidInviteCode;
}

public static class GroupErrors
{
    public static readonly Error NotFound = Error.NotFound("Group.NotFound", "The group does not exist.");
    public static readonly Error InvalidName = Error.Validation("Group.InvalidName", $"Name is required and must be at most {HikingGroup.MaxNameLength} characters.");
    public static readonly Error TextTooLong = Error.Validation("Group.TextTooLong", "Text is too long.");
    public static readonly Error InvalidInviteCode = Error.Validation("Group.InvalidInviteCode", "The invite code is not valid.");
    public static readonly Error AlreadyMember = Error.Conflict("Group.AlreadyMember", "You're already in this group.");
    public static readonly Error GroupFull = Error.Conflict("Group.Full", $"A group can have at most {HikingGroup.MaxMembers} members.");
    public static readonly Error NotMember = Error.Forbidden("Group.NotMember", "You're not a member of this group.");
    public static readonly Error NotOwner = Error.Forbidden("Group.NotOwner", "Only the group owner can do that.");
    public static readonly Error OwnerCannotLeave = Error.Conflict("Group.OwnerCannotLeave", "Transfer ownership before leaving, or delete the group.");
    public static readonly Error InvalidHike = Error.Validation("Group.InvalidHike", "The hike needs a title; notes must be short.");
    public static readonly Error HikeInPast = Error.Validation("Group.HikeInPast", "A group hike must be scheduled in the future.");
    public static readonly Error HikeNotFound = Error.NotFound("Group.HikeNotFound", "The group hike does not exist.");
    public static readonly Error HikeClosed = Error.Conflict("Group.HikeClosed", "This hike is cancelled or has already started.");
}
