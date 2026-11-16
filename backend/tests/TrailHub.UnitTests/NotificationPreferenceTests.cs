using TrailHub.Contracts;
using TrailHub.Notifications.Domain.Notifications;
using TrailHub.Notifications.Domain.Preferences;

namespace TrailHub.UnitTests;

public sealed class NotificationPreferenceTests
{
    private static readonly Guid Hike = Guid.NewGuid();

    [Fact]
    public void Everything_is_on_by_default()
    {
        var prefs = NotificationPreferences.Default("anna");

        Assert.All(Enum.GetValues<NotificationCategory>(), c => Assert.True(prefs.AllowsPush(c)));
    }

    [Fact]
    public async Task A_switched_off_category_still_reaches_the_inbox_but_not_the_phone()
    {
        var h = new NotificationsHarness();
        h.AddDevice("anna");
        var prefs = NotificationPreferences.Default("anna");
        prefs.Update(likesComments: false, group: true, trips: true);
        h.Preferences.Add(prefs);

        await h.Feed.HikeLikedAsync(new HikeLikedIntegrationEvent(Hike, "anna", "Rysy ridge", "ben", "Ben"), default);
        await h.Trips.GearUnclaimedAsync(new TripGearUnclaimedIntegrationEvent(Guid.NewGuid(), "Tatra traverse", Guid.NewGuid(), 2, new DateOnly(2026, 9, 3), ["anna"]), default);

        Assert.Equal(2, h.InboxOf("anna").Count);
        var push = Assert.Single(h.Sender.Sent);
        Assert.Equal("2 group items still unclaimed for Tatra traverse", push.Body);
    }

    [Fact]
    public async Task No_devices_means_no_push()
    {
        var h = new NotificationsHarness();

        await h.Feed.HikeLikedAsync(new HikeLikedIntegrationEvent(Hike, "anna", "Rysy ridge", "ben", "Ben"), default);

        Assert.Single(h.InboxOf("anna"));
        Assert.Empty(h.Sender.Sent);
    }

    [Fact]
    public async Task Pushes_go_to_every_device_and_gone_tokens_are_forgotten()
    {
        var h = new NotificationsHarness();
        var phone = h.AddDevice("anna");
        h.Devices.Add(TrailHub.Notifications.Domain.Devices.PushDevice.Register("ExponentPushToken[old]", "anna", "android", h.Clock.Now).Value);
        h.Sender.Unregistered.Add("ExponentPushToken[old]");

        await h.Feed.HikeLikedAsync(new HikeLikedIntegrationEvent(Hike, "anna", "Rysy ridge", "ben", "Ben"), default);

        Assert.Equal(2, h.Sender.Sent.Count);
        Assert.Equal([phone], h.Devices.Items.Select(d => d.Token));
    }
}
