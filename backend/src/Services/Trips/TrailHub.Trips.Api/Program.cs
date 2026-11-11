using TrailHub.BuildingBlocks.Web;
using TrailHub.Trips.Api;
using TrailHub.Trips.Application;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Trips API");
builder.Services
    .AddTripsApplication()
    .AddTripsInfrastructure(builder.Configuration);

var signalR = builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 4 * 1024);
if (builder.Configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
{
    // Backplane: lets hub messages reach clients connected to other instances of this service.
    signalR.AddStackExchangeRedis(redis, o => o.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("trailhub-trips"));
}

builder.Services.AddSingleton<ITripChangeNotifier, HubTripChangeNotifier>();

var app = builder.Build();
app.UseTrailHubServiceDefaults();
app.MapHub<TripHub>("/hubs/trips");

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateTripsDatabaseAsync();
}

await app.RunAsync();
