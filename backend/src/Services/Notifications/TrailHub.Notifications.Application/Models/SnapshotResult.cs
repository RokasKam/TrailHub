namespace TrailHub.Notifications.Application.Models;

/// <summary>
/// What applying a membership snapshot did. <paramref name="PreviousMemberIds"/> is who was in the group before, or
/// null when this was the first snapshot of the group.
/// </summary>
public sealed record SnapshotResult(bool Applied, IReadOnlyList<string>? PreviousMemberIds)
{
    public static readonly SnapshotResult Stale = new(false, null);
}
