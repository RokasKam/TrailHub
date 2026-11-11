using TrailHub.Activity.Application.Models;

namespace TrailHub.Activity.Application.Services;

/// <summary>
/// Names people the way their group knows them (the display name they joined with): preferably from the groups a
/// post is shared with, then any of the viewer's groups, then any group at all.
/// </summary>
internal sealed class DisplayNames(IReadOnlyList<GroupInfo> viewerGroups, IReadOnlyDictionary<string, string> otherGroups)
{
    /// <summary>For someone this service has never seen in a group.</summary>
    public const string Unknown = "Hiker";

    /// <summary>People not found in <paramref name="viewerGroups"/>, whose names must come from other groups.</summary>
    public static IReadOnlyCollection<string> Missing(IReadOnlyList<GroupInfo> viewerGroups, IEnumerable<string> userIds) =>
        userIds.Distinct().Where(id => !viewerGroups.Any(g => g.IsMember(id))).ToList();

    public string For(string userId, IReadOnlyCollection<Guid> preferredGroupIds) =>
        viewerGroups.Where(g => preferredGroupIds.Contains(g.Id)).Select(g => g.NameOf(userId)).FirstOrDefault(n => n is not null)
        ?? viewerGroups.Select(g => g.NameOf(userId)).FirstOrDefault(n => n is not null)
        ?? otherGroups.GetValueOrDefault(userId)
        ?? Unknown;
}
