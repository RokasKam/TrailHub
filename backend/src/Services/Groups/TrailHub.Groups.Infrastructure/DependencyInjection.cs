using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Groups.Application.Interfaces.Providers;
using TrailHub.Groups.Application.Interfaces.Repository;
using TrailHub.Groups.Infrastructure.BackgroundJobs;
using TrailHub.Groups.Infrastructure.LiveLocations;
using TrailHub.Groups.Infrastructure.Repositories;

namespace TrailHub.Groups.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGroupsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GroupsDb")
            ?? throw new InvalidOperationException("Connection string 'GroupsDb' is not configured.");

        services.AddDbContext<GroupsDbContext>(o => o.UseGroupsNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GroupsDbContext>());
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddSingleton<ILiveLocationStore, DistributedLiveLocationStore>();

        if (configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "trailhub:groups:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddTrailHubMessaging(configuration);
        services.AddHostedService<GroupMembersResync>();
        services.AddHealthChecks().AddDbContextCheck<GroupsDbContext>("groups-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateGroupsDatabaseAsync(this IServiceProvider services) =>
        services.MigrateDatabaseAsync<GroupsDbContext>();

    internal static DbContextOptionsBuilder UseGroupsNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "groups"));
}
