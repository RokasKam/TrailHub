using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Profile.Application.Profiles;
using TrailHub.Profile.Infrastructure.Messaging;
using TrailHub.Profile.Infrastructure.Persistence;

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

internal sealed class ProfileDbContextFactory : IDesignTimeDbContextFactory<ProfileDbContext>
{
    public ProfileDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProfileDbContext>();
        options.UseProfileNpgsql("Host=localhost;Database=trailhub_profile;Username=trailhub;Password=trailhub");
        return new ProfileDbContext(options.Options);
    }
}
