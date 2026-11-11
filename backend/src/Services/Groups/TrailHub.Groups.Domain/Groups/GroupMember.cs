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

    /// <summary>Snapshot of the member's profile name at join time; the Profile service owns the real one.</summary>
    public string DisplayName { get; private set; } = null!;

    public GroupRole Role { get; internal set; }

    public DateTime JoinedAtUtc { get; private set; }
}
