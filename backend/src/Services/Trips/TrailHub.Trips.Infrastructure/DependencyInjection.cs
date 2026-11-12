using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Infrastructure.Consumers;
using TrailHub.Trips.Infrastructure.Groups;
using TrailHub.Trips.Infrastructure.Persistence;
using TrailHub.Trips.Infrastructure.Providers.Frankfurter;
using TrailHub.Trips.Infrastructure.Repositories;

namespace TrailHub.Trips.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTripsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TripsDb")
            ?? throw new InvalidOperationException("Connection string 'TripsDb' is not configured.");

        services.AddDbContext<TripsDbContext>(o => o.UseTripsNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TripsDbContext>());
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IGroupDirectory, GroupDirectory>();
        services.AddScoped<IGearListRepository, GearListRepository>();
        services.AddScoped<ITripExpenseRepository, TripExpenseRepository>();
        services.AddScoped<ITripPaymentRepository, TripPaymentRepository>();

        // Exchange-rate suggestions: a short timeout, since the app lets people type a rate when this is slow or down.
        services.AddMemoryCache();
        var ratesBase = configuration["ExchangeRates:BaseUrl"] is { Length: > 0 } url ? url : "https://api.frankfurter.dev/";
        services
            .AddHttpClient<IExchangeRateProvider, FrankfurterExchangeRateProvider>(http =>
            {
                http.BaseAddress = new Uri(ratesBase.EndsWith('/') ? ratesBase : ratesBase + "/");
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TrailHub/1.0 (trip expenses)");
                http.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler(static (HttpStandardResilienceOptions o) =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
                o.Retry.MaxRetryAttempts = 1;
            });

        // One membership snapshot at a time: a new group and its first join arrive together, and processing them in
        // parallel makes both insert the same group (a duplicate-key failure, then a retry that briefly leaves the
        // read model stale).
        services.AddTrailHubMessaging(configuration, bus => bus.AddConsumer<GroupMembersChangedConsumer>().Endpoint(e => e.ConcurrentMessageLimit = 1));
        services.AddHealthChecks().AddDbContextCheck<TripsDbContext>("trips-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateTripsDatabaseAsync(this IServiceProvider services) =>
        services.MigrateDatabaseAsync<TripsDbContext>();

    internal static DbContextOptionsBuilder UseTripsNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trips"));
}
