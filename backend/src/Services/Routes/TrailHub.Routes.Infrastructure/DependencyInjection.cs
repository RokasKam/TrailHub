using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.BuildingBlocks.Infrastructure;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Routes.Application.Routes;
using TrailHub.Routes.Domain.Routes;
using TrailHub.Routes.Infrastructure.Providers;

namespace TrailHub.Routes.Infrastructure;

/// <summary>Which service draws routes along paths (<c>Routing:Provider</c>).</summary>
internal enum RoutingProvider
{
    /// <summary>Mapbox walking directions: the default whenever a Mapbox token is configured.</summary>
    Mapbox,

    /// <summary>OpenRouteService's hiking profile, which also reports SAC trail grades. Needs its own key.</summary>
    OpenRouteService,

    /// <summary>No routing service: straight lines between waypoints.</summary>
    StraightLine,
}

internal sealed record RoutingOptions(RoutingProvider Provider, string? OpenRouteServiceApiKey)
{
    public static RoutingOptions From(IConfiguration configuration, MapboxOptions mapbox)
    {
        var orsKey = configuration["Routing:OpenRouteService:ApiKey"];
        var provider = Enum.TryParse<RoutingProvider>(configuration["Routing:Provider"], ignoreCase: true, out var chosen) ? chosen
            : mapbox.Configured ? RoutingProvider.Mapbox
            : !string.IsNullOrWhiteSpace(orsKey) ? RoutingProvider.OpenRouteService
            : RoutingProvider.StraightLine;

        // A provider without its credentials can't work; fall back rather than fail every request.
        if ((provider == RoutingProvider.Mapbox && !mapbox.Configured) || (provider == RoutingProvider.OpenRouteService && string.IsNullOrWhiteSpace(orsKey)))
        {
            provider = RoutingProvider.StraightLine;
        }

        return new RoutingOptions(provider, orsKey);
    }
}

public sealed class RoutesDbContext(DbContextOptions<RoutesDbContext> options) : DbContext(options), IUnitOfWork
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DbSet<PlannedRoute> Routes => Set<PlannedRoute>();

    internal DbSet<KnownGroup> KnownGroups => Set<KnownGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("routes");

        var route = modelBuilder.Entity<PlannedRoute>();
        route.HasKey(r => r.Id);
        route.Property(r => r.OwnerId).HasMaxLength(128).IsRequired();
        route.Property(r => r.Name).HasMaxLength(PlannedRoute.MaxNameLength).IsRequired();
        route.Property(r => r.Description).HasMaxLength(PlannedRoute.MaxTextLength);
        route.HasIndex(r => new { r.OwnerId, r.UpdatedAtUtc });
        route.Ignore(r => r.DayCount);
        route.Property<uint>("xmin").IsRowVersion();

        // Waypoints and the routed line are always read and written whole, so they live in jsonb columns.
        route.Ignore(r => r.Waypoints);
        route.Property<List<RouteWaypoint>>("_waypoints")
            .HasColumnName("Waypoints")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<RouteWaypoint>>(v, Json) ?? new List<RouteWaypoint>(),
                JsonComparer<List<RouteWaypoint>>());

        // [lat, lon, elevation] triples: about a third of the size of named fields for a long route.
        route.Ignore(r => r.Points);
        route.Property<List<GeoPoint>>("_points")
            .HasColumnName("Points")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Select(p => new[] { p.Latitude, p.Longitude, p.Elevation }), Json),
                v => (JsonSerializer.Deserialize<double?[][]>(v, Json) ?? Array.Empty<double?[]>()).Select(p => new GeoPoint(p[0]!.Value, p[1]!.Value, p[2])).ToList(),
                new ValueComparer<List<GeoPoint>>((a, b) => ReferenceEquals(a, b), v => v.Count, v => v));

        route.Property(r => r.SacScale);
        route.Property(r => r.WaypointIndices);
        route.Property(r => r.SharedWithGroups); // uuid[]
        route.HasIndex(r => r.SharedWithGroups).HasMethod("gin"); // "shared with any of my groups" lookups

        var group = modelBuilder.Entity<KnownGroup>();
        group.ToTable("KnownGroups");
        group.HasKey(g => g.Id);
        group.Property(g => g.Id).ValueGeneratedNever();
        group.Property(g => g.Name).HasMaxLength(100).IsRequired();
        group.OwnsMany(g => g.Members, member =>
        {
            member.ToTable("KnownGroupMembers");
            member.WithOwner().HasForeignKey("GroupId");
            member.HasKey("GroupId", nameof(KnownGroupMember.UserId));
            member.Property(m => m.UserId).HasMaxLength(128);
            member.Property(m => m.DisplayName).HasMaxLength(100).IsRequired();
            member.HasIndex(m => m.UserId);
        });
    }

    private static ValueComparer<T> JsonComparer<T>()
        where T : class => new(
            (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
            v => JsonSerializer.Serialize(v, Json).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Json), Json)!);
}

internal sealed class RouteRepository(RoutesDbContext db) : IRouteRepository
{
    public Task<PlannedRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RouteSummary>> GetSummariesVisibleToAsync(string userId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        // Skips the (large) routed line; waypoints are enough for the start point and day count.
        var rows = await db.Routes.AsNoTracking()
            .Where(r => r.OwnerId == userId || r.SharedWithGroups.Any(g => groupIds.Contains(g)))
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Select(r => new { r.Id, r.Name, r.OwnerId, r.DistanceMeters, r.AscentMeters, r.DurationMinutes, r.Snapped, r.UpdatedAtUtc, r.PublishedAtUtc, Waypoints = EF.Property<List<RouteWaypoint>>(r, "_waypoints") })
            .ToListAsync(cancellationToken);
        return rows
            .Select(r => new RouteSummary(
                r.Id,
                r.Name,
                r.DistanceMeters,
                r.AscentMeters,
                r.DurationMinutes,
                r.Waypoints.Count(w => w.IsOvernight) + 1,
                r.Snapped,
                r.Waypoints[0].Latitude,
                r.Waypoints[0].Longitude,
                r.UpdatedAtUtc,
                r.OwnerId,
                r.OwnerId == userId,
                r.PublishedAtUtc is not null))
            .ToList();
    }

    public void Add(PlannedRoute route) => db.Routes.Add(route);

    public void Remove(PlannedRoute route) => db.Routes.Remove(route);
}

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
        services.AddTrailHubMessaging(configuration, bus => bus.AddConsumer<GroupMembersChangedConsumer>());
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

internal sealed class RoutesDbContextFactory : IDesignTimeDbContextFactory<RoutesDbContext>
{
    public RoutesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RoutesDbContext>();
        options.UseRoutesNpgsql("Host=localhost;Database=trailhub_routes;Username=trailhub;Password=trailhub");
        return new RoutesDbContext(options.Options);
    }
}
