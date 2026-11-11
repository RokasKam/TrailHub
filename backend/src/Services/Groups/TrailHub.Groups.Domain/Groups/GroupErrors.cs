using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Groups.Domain.Groups;

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
