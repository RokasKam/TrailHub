using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.Trails.Application.Abstractions;
using TrailHub.Trails.Application.Community;
using TrailHub.Trails.Application.Trails;
using TrailHub.Trails.Infrastructure.Persistence;
using TrailHub.Trails.Infrastructure.Photos;

namespace TrailHub.Trails.Infrastructure;

/// <summary>Moderators are listed in configuration (<c>Moderation:ModeratorUserIds</c>, Auth0 user ids).</summary>
internal sealed class ConfiguredModerators(IReadOnlyCollection<string> userIds) : IModerators
{
    private readonly HashSet<string> _ids = [.. userIds.Where(id => !string.IsNullOrWhiteSpace(id))];

    public bool IsModerator(string userId) => _ids.Contains(userId);
}

public static class DependencyInjection
{
    public static IServiceCollection AddTrailsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TrailsDb")
            ?? throw new InvalidOperationException("Connection string 'TrailsDb' is not configured.");

        services.AddDbContext<TrailsDbContext>(o => o.UseTrailsNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TrailsDbContext>());
        services.AddScoped<ITrailRepository, TrailRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IConditionReportRepository, ConditionReportRepository>();
        services.AddScoped<IFavoriteTrailRepository, FavoriteTrailRepository>();

        services.AddSingleton<IPhotoStorage, S3PhotoStorage>();
        services.AddGpxSupport();
        services.AddScoped<ITrailReportRepository, TrailReportRepository>();
        services.AddSingleton<IModerators>(new ConfiguredModerators(configuration.GetSection("Moderation:ModeratorUserIds").Get<string[]>() ?? []));
        services.AddTrailHubMessaging(configuration, bus =>
        {
            bus.AddConsumer<Messaging.RoutePublishedConsumer>();
            bus.AddConsumer<Messaging.RouteUnpublishedConsumer>();
        });
        services.AddHealthChecks().AddDbContextCheck<TrailsDbContext>("trails-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateTrailsDatabaseAsync(this IServiceProvider services) =>
        BuildingBlocks.Infrastructure.Persistence.PersistenceExtensions.MigrateDatabaseAsync<TrailsDbContext>(services);

    internal static DbContextOptionsBuilder UseTrailsNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trails");
        });
}

/// <summary>Used by <c>dotnet ef</c> only; runtime configuration comes from the API host.</summary>
internal sealed class TrailsDbContextFactory : IDesignTimeDbContextFactory<TrailsDbContext>
{
    public TrailsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrailsDbContext>();
        options.UseTrailsNpgsql("Host=localhost;Database=trailhub_trails;Username=trailhub;Password=trailhub");
        return new TrailsDbContext(options.Options);
    }
}
