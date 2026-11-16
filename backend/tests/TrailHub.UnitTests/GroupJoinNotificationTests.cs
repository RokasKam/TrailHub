using TrailHub.Notifications.Application.Models;
using TrailHub.Notifications.Application.Requests;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.UnitTests;

public sealed class GroupJoinNotificationTests
{
    private static readonly Guid Group = Guid.NewGuid();

    private static SyncGroupMembersRequest Snapshot(DateTime at, params string[] members) =>
        new(Group, "Friends", members.Select(m => new GroupMemberInfo(m, char.ToUpperInvariant(m[0]) + m[1..])).ToList(), false, at);

    [Fact]
    public async Task The_first_snapshot_notifies_nobody()
    {
        var h = new NotificationsHarness();

        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now, "anna"), default);

        Assert.Empty(h.Notifications.Items);
    }

    [Fact]
    public async Task Existing_members_hear_about_someone_joining()
    {
        var h = new NotificationsHarness();
        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now, "anna"), default);
        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now.AddSeconds(1), "anna", "ben"), default);
        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now.AddSeconds(2), "anna", "ben", "cleo"), default);

        Assert.Equal(["Ben joined Friends", "Cleo joined Friends"], h.InboxOf("anna").Select(n => n.Body));
        Assert.Equal(["Cleo joined Friends"], h.InboxOf("ben").Select(n => n.Body));
        Assert.Empty(h.InboxOf("cleo"));
        Assert.All(h.Notifications.Items, n => Assert.Equal(NotificationKind.GroupMemberJoined, n.Kind));
        Assert.All(h.Notifications.Items, n => Assert.Equal($"/groups/{Group}", n.TargetUrl));
    }

    [Fact]
    public async Task Older_snapshots_are_ignored()
    {
        var h = new NotificationsHarness();
        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now, "anna"), default);
        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now.AddSeconds(2), "anna", "ben"), default);

        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now.AddSeconds(1), "anna", "cleo"), default);

        Assert.Equal(["Ben joined Friends"], h.InboxOf("anna").Select(n => n.Body));
    }

    [Fact]
    public async Task Leaving_notifies_nobody()
    {
        var h = new NotificationsHarness();
        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now, "anna", "ben"), default);

        await h.Membership.SyncGroupMembersAsync(Snapshot(h.Clock.Now.AddSeconds(1), "anna"), default);

        Assert.Empty(h.Notifications.Items);
    }
}
