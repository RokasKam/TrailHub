using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Infrastructure.Consumers;
using TrailHub.Notifications.Infrastructure.Groups;
using TrailHub.Notifications.Infrastructure.Persistence;
using TrailHub.Notifications.Infrastructure.Push;
using TrailHub.Notifications.Infrastructure.Repositories;

namespace TrailHub.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationsDb")
            ?? throw new InvalidOperationException("Connection string 'NotificationsDb' is not configured.");

        services.AddDbContext<NotificationsDbContext>(o => o.UseNotificationsNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPushDeviceRepository, PushDeviceRepository>();
        services.AddScoped<INotificationPreferencesRepository, NotificationPreferencesRepository>();
        services.AddScoped<IGroupDirectory, GroupDirectory>();

        AddPush(services, configuration);

        services.AddTrailHubMessaging(configuration, bus =>
        {
            // One membership snapshot at a time: a new group and its first join arrive together, and joins are found by
            // comparing each snapshot with the one before, so they must be applied in order.
            bus.AddConsumer<GroupMembersChangedConsumer>().Endpoint(e => e.ConcurrentMessageLimit = 1);

            // Likes on one hike update the same inbox item; one at a time avoids conflicting updates and retries.
            bus.AddConsumer<HikeLikedConsumer>().Endpoint(e => e.ConcurrentMessageLimit = 1);
            bus.AddConsumer<HikeCommentedConsumer>();
            bus.AddConsumer<HikeSharedConsumer>();
            bus.AddConsumer<GroupHikeScheduledConsumer>();
            bus.AddConsumer<TripExpenseAddedConsumer>();
            bus.AddConsumer<TripPaymentRecordedConsumer>();
            bus.AddConsumer<TripGearUnclaimedConsumer>();
        });
        services.AddHealthChecks().AddDbContextCheck<NotificationsDbContext>("notifications-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateNotificationsDatabaseAsync(this IServiceProvider services) =>
        services.MigrateDatabaseAsync<NotificationsDbContext>();

    internal static DbContextOptionsBuilder UseNotificationsNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"));

    /// <summary>Expo push (section <c>Push</c>); <c>Push:Enabled=false</c> only logs what would be sent.</summary>
    private static void AddPush(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue("Push:Enabled", true))
        {
            services.AddSingleton<IPushSender, LoggingPushSender>();
            return;
        }

        var baseUrl = configuration["Push:Expo:BaseUrl"] is { Length: > 0 } url ? url : "https://exp.host/";
        var accessToken = configuration["Push:Expo:AccessToken"];
        services
            .AddHttpClient<IPushSender, ExpoPushSender>(http =>
            {
                http.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                http.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    // Needed when "enhanced push security" is on for the Expo project.
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            // Expo asks clients to retry 429 and 5xx with back-off; the standard handler does exactly that.
            .AddStandardResilienceHandler(static (HttpStandardResilienceOptions o) =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                o.Retry.MaxRetryAttempts = 2;
            });
    }
}
