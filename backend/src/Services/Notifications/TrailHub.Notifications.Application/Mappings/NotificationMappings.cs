using TrailHub.Notifications.Application.Responses;
using TrailHub.Notifications.Domain.Notifications;
using TrailHub.Notifications.Domain.Preferences;

namespace TrailHub.Notifications.Application.Mappings;

internal static class NotificationMappings
{
    public static NotificationResponse ToResponse(this Notification n) =>
        new(n.Id, n.Category, n.Kind, n.Title, n.Body, n.TargetUrl, n.CreatedAtUtc, n.UpdatedAtUtc, n.IsRead);

    public static PreferencesResponse ToResponse(this NotificationPreferences p) => new(p.LikesComments, p.Group, p.Trips);
}
