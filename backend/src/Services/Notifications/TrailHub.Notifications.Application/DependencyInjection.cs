using Microsoft.Extensions.DependencyInjection;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Application.Services;

namespace TrailHub.Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services) =>
        services
            .AddScoped<NotificationDispatcher>()
            .AddScoped<INotificationService, NotificationService>()
            .AddScoped<IPushDeviceService, PushDeviceService>()
            .AddScoped<INotificationPreferencesService, NotificationPreferencesService>()
            .AddScoped<IFeedNotificationService, FeedNotificationService>()
            .AddScoped<IGroupNotificationService, GroupNotificationService>()
            .AddScoped<IGroupMembershipService, GroupMembershipService>()
            .AddScoped<ITripNotificationService, TripNotificationService>();
}
