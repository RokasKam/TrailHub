using TrailHub.BuildingBlocks.Domain;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Domain.Preferences;

/// <summary>Which categories a user gets pushes for (all on by default). The inbox records everything regardless.</summary>
public sealed class NotificationPreferences : AggregateRoot<string>
{
    private NotificationPreferences()
    {
    }

    public string UserId => Id;

    public bool LikesComments { get; private set; } = true;

    public bool Group { get; private set; } = true;

    public bool Trips { get; private set; } = true;

    public static NotificationPreferences Default(string userId) => new() { Id = userId };

    public void Update(bool likesComments, bool group, bool trips)
    {
        LikesComments = likesComments;
        Group = group;
        Trips = trips;
    }

    public bool AllowsPush(NotificationCategory category) => category switch
    {
        NotificationCategory.LikesComments => LikesComments,
        NotificationCategory.Group => Group,
        NotificationCategory.Trips => Trips,
        _ => false,
    };
}
