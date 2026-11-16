using TrailHub.Contracts;
using TrailHub.Notifications.Application.Models;
using TrailHub.Notifications.Application.Requests;

namespace TrailHub.UnitTests;

public sealed class SelfActionNotificationTests
{
    private static readonly Guid Hike = Guid.NewGuid();
    private static readonly Guid Trip = Guid.NewGuid();

    [Fact]
    public async Task Liking_or_commenting_on_your_own_hike_notifies_nobody()
    {
        var h = new NotificationsHarness();

        await h.Feed.HikeLikedAsync(new HikeLikedIntegrationEvent(Hike, "anna", "Rysy ridge", "anna", "Anna"), default);
        await h.Feed.HikeCommentedAsync(new HikeCommentedIntegrationEvent(Hike, "anna", "Rysy ridge", Guid.NewGuid(), "anna", "Anna", "Mine"), default);

        Assert.Empty(h.Notifications.Items);
    }

    [Fact]
    public async Task Sharing_tells_the_other_members_once()
    {
        var h = new NotificationsHarness();
        var friends = Guid.NewGuid();
        var club = Guid.NewGuid();
        await h.Groups.ApplySnapshotAsync(friends, "Friends", [new("anna", "Anna"), new("ben", "Ben")], false, h.Clock.Now, default);
        await h.Groups.ApplySnapshotAsync(club, "Club", [new("anna", "Anna"), new("ben", "Ben"), new("cleo", "Cleo")], false, h.Clock.Now, default);

        await h.Feed.HikeSharedAsync(new HikeSharedIntegrationEvent(Hike, "anna", "Anna", "Rysy ridge", [friends, club]), default);
        await h.Feed.HikeSharedAsync(new HikeSharedIntegrationEvent(Hike, "anna", "Anna", "Rysy ridge", [club]), default); // redelivered

        Assert.Empty(h.InboxOf("anna"));
        var ben = Assert.Single(h.InboxOf("ben"));
        Assert.Equal("New hike in Friends", ben.Title);
        Assert.Equal("Anna shared Rysy ridge", ben.Body);
        Assert.Single(h.InboxOf("cleo"));
    }

    [Fact]
    public async Task Expenses_notify_share_holders_but_not_the_creator()
    {
        var h = new NotificationsHarness();

        await h.Trips.ExpenseAddedAsync(
            new TripExpenseAddedIntegrationEvent(Trip, "Tatra traverse", Guid.NewGuid(), "Hut dinner", "ben", "Ben", "EUR",
                [new("ben", 30m), new("anna", 30m), new("cleo", 30m)]),
            default);

        Assert.Empty(h.InboxOf("ben"));
        var anna = Assert.Single(h.InboxOf("anna"));
        Assert.Equal("Ben added Hut dinner · your share €30.00", anna.Body);
        Assert.Equal("Tatra traverse", anna.Title);
        Assert.Equal($"/trips/{Trip}/expenses", anna.TargetUrl);
        Assert.Single(h.InboxOf("cleo"));
    }

    [Fact]
    public async Task Payments_notify_whichever_side_did_not_record_it()
    {
        var h = new NotificationsHarness();

        await h.Trips.PaymentRecordedAsync(new TripPaymentRecordedIntegrationEvent(Trip, "Tatra", Guid.NewGuid(), "ben", "Ben", "anna", "Anna", 12.5m, "CHF", "ben"), default);

        Assert.Empty(h.InboxOf("ben"));
        Assert.Equal("Ben paid you CHF 12.50", Assert.Single(h.InboxOf("anna")).Body);
    }

    [Fact]
    public async Task Payments_recorded_by_someone_else_notify_both()
    {
        var h = new NotificationsHarness();

        await h.Trips.PaymentRecordedAsync(new TripPaymentRecordedIntegrationEvent(Trip, "Tatra", Guid.NewGuid(), "ben", "Ben", "anna", "Anna", 5m, "EUR", "cleo"), default);

        Assert.Single(h.InboxOf("anna"));
        Assert.Equal("Your payment of €5.00 to Anna was recorded", Assert.Single(h.InboxOf("ben")).Body);
    }

    [Fact]
    public async Task Group_hikes_notify_members_but_not_the_scheduler()
    {
        var h = new NotificationsHarness();
        var group = Guid.NewGuid();
        await h.Membership.SyncGroupMembersAsync(new SyncGroupMembersRequest(group, "Friends", [new GroupMemberInfo("anna", "Anna"), new GroupMemberInfo("ben", "Ben")], false, h.Clock.Now), default);

        await h.GroupEvents.GroupHikeScheduledAsync(
            new GroupHikeScheduledIntegrationEvent(group, Guid.NewGuid(), "Sunrise", new DateTime(2026, 9, 5, 5, 0, 0, DateTimeKind.Utc), ["anna", "ben"], "Friends", "anna"),
            default);

        Assert.Empty(h.InboxOf("anna"));
        var ben = Assert.Single(h.InboxOf("ben"));
        Assert.Equal("Group hike in Friends", ben.Title);
        Assert.Equal("Anna scheduled Sunrise for Sat 5 Sep", ben.Body);
    }
}
