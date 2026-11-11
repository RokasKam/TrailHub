using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Interfaces.Repository;
using TrailHub.Routes.Infrastructure.Consumers;
using TrailHub.Routes.Infrastructure.Providers;
using TrailHub.Routes.Infrastructure.Providers.Mapbox;
using TrailHub.Routes.Infrastructure.Providers.OpenMeteo;
using TrailHub.Routes.Infrastructure.Providers.OpenRouteService;
using TrailHub.Routes.Infrastructure.Providers.Overpass;
using TrailHub.Routes.Infrastructure.Providers.Photon;
using TrailHub.Routes.Infrastructure.Repositories;

namespace TrailHub.Routes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRoutesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RoutesDb")
            ?? throw new InvalidOperationException("Connection string 'RoutesDb' is not configured.");

        services.AddDbContext<RoutesDbContext>(o => o.UseRoutesNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<RoutesDbContext>());
        services.AddScoped<IRouteRepository, RouteRepository>();
        services.AddScoped<IGroupDirectory, GroupDirectory>();
        // One membership snapshot at a time: a new group and its first join arrive together, and processing them in
        // parallel makes both insert the same group (a duplicate-key failure, then a retry that briefly leaves the
        // read model stale).
        services.AddTrailHubMessaging(configuration, bus => bus.AddConsumer<GroupMembersChangedConsumer>().Endpoint(e => e.ConcurrentMessageLimit = 1));
        services.AddGpxSupport();

        if (configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "trailhub:routes:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        var mapbox = new MapboxOptions(configuration["Routing:Mapbox:AccessToken"]);
        var options = RoutingOptions.From(configuration, mapbox);
        services.AddSingleton(mapbox);
        services.AddSingleton(options);

        // Third-party map services are slow at times: give each attempt longer than the default.
        static void Patient(HttpStandardResilienceOptions o)
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(75);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            o.Retry.MaxRetryAttempts = 1;
        }

        var mapboxBase = new Uri(configuration["Routing:Mapbox:BaseUrl"] ?? "https://api.mapbox.com/");
        services.AddHttpClient<MapboxDirectionsRouter>(http => http.BaseAddress = mapboxBase).AddStandardResilienceHandler(Patient);
        services.AddHttpClient<MapboxGeocoder>(http => http.BaseAddress = mapboxBase).AddStandardResilienceHandler();
        services.AddHttpClient<MapboxPoiSource>(http => http.BaseAddress = mapboxBase).AddStandardResilienceHandler();
        services.AddHttpClient<OpenRouteServiceRouter>(http => http.BaseAddress = new Uri(configuration["Routing:OpenRouteService:BaseUrl"] ?? "https://api.openrouteservice.org/"))
            .AddStandardResilienceHandler(Patient);
        services.AddHttpClient<OpenMeteoElevation>(http => http.BaseAddress = new Uri(configuration["Routing:OpenMeteo:BaseUrl"] ?? "https://api.open-meteo.com/"))
            .AddStandardResilienceHandler(o =>
            {
                Patient(o);
                o.Retry.MaxRetryAttempts = 3; // Open-Meteo answers bursts with 429; backing off a few seconds is enough
                o.Retry.Delay = TimeSpan.FromSeconds(2);
            });
        services.AddHttpClient<MapboxTerrainElevation>(http => http.BaseAddress = mapboxBase).AddStandardResilienceHandler();
        services.AddTransient<IElevationSource, ElevationSource>();
        services.AddTransient<StraightLineRouter>();

        services.AddSingleton(new OverpassOptions(
            (configuration.GetSection("Routing:Overpass:Servers").Get<string[]>() is { Length: > 0 } servers
                ? servers
                : ["https://overpass-api.de/", "https://overpass.private.coffee/", "https://maps.mail.ru/osm/tools/overpass/"])
            .Select(s => new Uri(s.EndsWith('/') ? s : s + "/"))
            .ToList()));
        services.AddHttpClient<OverpassPoiSource>(http =>
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TrailHub/1.0 (route planner)");
                http.Timeout = TimeSpan.FromSeconds(20); // per server; the next one is tried after that
            });
        services.AddHttpClient<PhotonGeocoder>(http =>
            {
                http.BaseAddress = new Uri(configuration["Routing:Photon:BaseUrl"] ?? "https://photon.komoot.io/");
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TrailHub/1.0 (route planner)");
            })
            .AddStandardResilienceHandler();

        services.AddScoped<IPoiSource, FallbackPoiSource>();
        services.AddScoped<IGeocoder, FallbackGeocoder>();
        services.AddScoped<IRouter>(sp => new CachingRouter(
            options.Provider switch
            {
                RoutingProvider.Mapbox => sp.GetRequiredService<MapboxDirectionsRouter>(),
                RoutingProvider.OpenRouteService => sp.GetRequiredService<OpenRouteServiceRouter>(),
                _ => sp.GetRequiredService<StraightLineRouter>(),
            },
            sp.GetRequiredService<IDistributedCache>(),
            options.Provider.ToString().ToLowerInvariant()));

        services.AddHealthChecks().AddDbContextCheck<RoutesDbContext>("routes-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateRoutesDatabaseAsync(this IServiceProvider services) =>
        services.MigrateDatabaseAsync<RoutesDbContext>();

    internal static DbContextOptionsBuilder UseRoutesNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "routes"));
}
