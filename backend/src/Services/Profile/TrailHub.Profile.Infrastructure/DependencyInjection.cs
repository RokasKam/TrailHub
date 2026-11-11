using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Profile.Application.Interfaces.Repository;
using TrailHub.Profile.Infrastructure.Messaging;
using TrailHub.Profile.Infrastructure.Persistence;
using TrailHub.Profile.Infrastructure.Repositories;

namespace TrailHub.Profile.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProfileInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ProfileDb")
            ?? throw new InvalidOperationException("Connection string 'ProfileDb' is not configured.");

        services.AddDbContext<ProfileDbContext>(o => o.UseProfileNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProfileDbContext>());
        services.AddScoped<IProfileRepository, ProfileRepository>();

        services.AddTrailHubMessaging(configuration, bus =>
        {
            bus.AddConsumer<ActivityCompletedConsumer>();
            bus.AddConsumer<ActivityDeletedConsumer>();
        });
        services.AddHealthChecks().AddDbContextCheck<ProfileDbContext>("profile-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateProfileDatabaseAsync(this IServiceProvider services) =>
        services.MigrateDatabaseAsync<ProfileDbContext>();

    internal static DbContextOptionsBuilder UseProfileNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "profile"));
}
