namespace TrailHub.Notifications.Domain.Notifications;

/// <summary>What a notification is about. Each category has its own push on/off switch; the inbox always records.</summary>
public enum NotificationCategory
{
    /// <summary>Likes and comments on your shared hikes.</summary>
    LikesComments,

    /// <summary>Group hikes scheduled, people joining, hikes shared with your groups.</summary>
    Group,

    /// <summary>Trip expenses, payments and gear reminders.</summary>
    Trips,
}
