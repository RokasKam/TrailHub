using TrailHub.BuildingBlocks.Domain;
using TrailHub.Notifications.Domain.Devices;

namespace TrailHub.Notifications.Domain.Notifications;

public static class NotificationErrors
{
    public static readonly Error NotFound = Error.NotFound("Notification.NotFound", "The notification does not exist.");
    public static readonly Error InvalidDevice = Error.Validation(
        "Notification.InvalidDevice",
        $"A device needs a push token (at most {PushDevice.MaxTokenLength} characters, no spaces) and a platform: ios or android.");
}
