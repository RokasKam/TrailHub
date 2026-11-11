using Microsoft.Extensions.DependencyInjection;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Services;

namespace TrailHub.Activity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddActivityApplication(this IServiceCollection services) =>
        services
            .AddScoped<IActivityService, ActivityService>()
            .AddScoped<IHikeSharingService, HikeSharingService>()
            .AddScoped<IHikePhotoService, HikePhotoService>()
            .AddScoped<IHikeFeedService, HikeFeedService>()
            .AddScoped<IGroupMembershipService, GroupMembershipService>();
}
