using TrailHub.Notifications.Application.Services;
using TrailHub.Notifications.Domain.Devices;

namespace TrailHub.UnitTests;

/// <summary>The Notifications application services wired to in-memory fakes.</summary>
internal sealed class NotificationsHarness
{
    public NotificationsHarness()
    {
        var dispatcher = new NotificationDispatcher(Notifications, Preferences, Devices, Sender, UnitOfWork, Clock);
        Feed = new FeedNotificationService(dispatcher, Notifications, Groups);
        Trips = new TripNotificationService(dispatcher);
        GroupEvents = new GroupNotificationService(dispatcher, Groups);
        Membership = new GroupMembershipService(Groups, dispatcher);
    }

    public FakeClock Clock { get; } = new(new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));

    public FakeNotificationRepository Notifications { get; } = new();

    public FakePreferencesRepository Preferences { get; } = new();

    public FakePushDeviceRepository Devices { get; } = new();

    public FakePushSender Sender { get; } = new();

    public FakeGroupDirectory Groups { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public FeedNotificationService Feed { get; }

    public TripNotificationService Trips { get; }

    public GroupNotificationService GroupEvents { get; }

    public GroupMembershipService Membership { get; }

    /// <summary>Gives the user a phone, so pushes to them show up in <see cref="FakePushSender.Sent"/>.</summary>
    public string AddDevice(string userId)
    {
        var token = $"ExponentPushToken[{userId}]";
        Devices.Add(PushDevice.Register(token, userId, "ios", Clock.Now).Value);
        return token;
    }

    public List<TrailHub.Notifications.Domain.Notifications.Notification> InboxOf(string userId) =>
        Notifications.Items.Where(n => n.UserId == userId).ToList();
}
