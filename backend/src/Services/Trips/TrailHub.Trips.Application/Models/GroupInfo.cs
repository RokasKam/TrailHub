namespace TrailHub.Trips.Application.Models;

/// <summary>A group as this service knows it, from <see cref="Interfaces.Providers.IGroupDirectory"/>.</summary>
public sealed record GroupInfo(Guid Id, string Name, IReadOnlyList<GroupMemberInfo> Members)
{
    public bool IsMember(string userId) => Members.Any(m => m.UserId == userId);

    public string? NameOf(string userId) => Members.FirstOrDefault(m => m.UserId == userId)?.DisplayName;
}
