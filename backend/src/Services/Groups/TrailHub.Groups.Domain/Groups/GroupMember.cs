namespace TrailHub.Groups.Domain.Groups;

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

    /// <summary>The member's profile name: given when they join, then kept current from the Profile service's renames.</summary>
    public string DisplayName { get; internal set; } = null!;

    public GroupRole Role { get; internal set; }

    public DateTime JoinedAtUtc { get; private set; }
}
