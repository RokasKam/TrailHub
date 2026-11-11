using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Infrastructure.Persistence;
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

        services.AddGpxSupport();
        services.AddTrailHubMessaging(configuration);
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
