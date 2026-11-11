using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Groups.Domain.Groups;

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
