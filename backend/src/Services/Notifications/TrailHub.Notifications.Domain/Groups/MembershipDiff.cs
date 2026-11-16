namespace TrailHub.Notifications.Domain.Groups;

/// <summary>Works out who joined a group from two consecutive membership snapshots.</summary>
public static class MembershipDiff
{
    /// <summary>
    /// People in <paramref name="current"/> who weren't in <paramref name="previous"/>. The very first snapshot of a
    /// group (<paramref name="previous"/> is null) has no joins: it's the creator, or a group this service is just
    /// catching up on.
    /// </summary>
    public static IReadOnlyList<string> Joined(IReadOnlyCollection<string>? previous, IReadOnlyCollection<string> current) =>
        previous is null ? [] : current.Distinct().Where(id => !previous.Contains(id)).ToList();
}
