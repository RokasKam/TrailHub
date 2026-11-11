using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.Activity.Application.Interfaces.Providers;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Infrastructure.Consumers;
using TrailHub.Activity.Infrastructure.Persistence;
using TrailHub.Activity.Infrastructure.Photos;
using TrailHub.Activity.Infrastructure.Repositories;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;

namespace TrailHub.Activity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddActivityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ActivityDb")
            ?? throw new InvalidOperationException("Connection string 'ActivityDb' is not configured.");

        services.AddDbContext<ActivityDbContext>(o => o.UseActivityNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ActivityDbContext>());
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IHikeLikeRepository, HikeLikeRepository>();
        services.AddScoped<IHikeCommentRepository, HikeCommentRepository>();
        services.AddScoped<IGroupDirectory, GroupDirectory>();
        services.AddSingleton<IHikePhotoStorage, S3HikePhotoStorage>();

        services.AddGpxSupport();

        // Group memberships come from the Groups service, which republishes every group on start so a fresh database catches up.
        // One membership snapshot at a time: a new group and its first join arrive together, and processing them in
        // parallel makes both insert the same group (a duplicate-key failure, then a retry that briefly leaves the
        // read model stale).
        services.AddTrailHubMessaging(configuration, bus => bus.AddConsumer<GroupMembersChangedConsumer>().Endpoint(e => e.ConcurrentMessageLimit = 1));
        services.AddHealthChecks().AddDbContextCheck<ActivityDbContext>("activity-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateActivityDatabaseAsync(this IServiceProvider services) =>
        services.MigrateDatabaseAsync<ActivityDbContext>();

    internal static DbContextOptionsBuilder UseActivityNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "activity");
        });
}
