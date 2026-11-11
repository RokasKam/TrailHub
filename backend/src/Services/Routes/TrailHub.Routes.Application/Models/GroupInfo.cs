namespace TrailHub.Routes.Application.Models;

public sealed record GroupInfo(Guid Id, string Name, IReadOnlyList<GroupMemberInfo> Members)
{
    public bool IsMember(string userId) => Members.Any(m => m.UserId == userId);
}
