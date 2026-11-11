using Microsoft.AspNetCore.SignalR;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Groups.Api.Hubs;
using TrailHub.Groups.Application;
using TrailHub.Groups.Application.Interfaces.Providers;
using TrailHub.Groups.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Groups API");
builder.Services
    .AddGroupsApplication()
    .AddGroupsInfrastructure(builder.Configuration);

var signalR = builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 16 * 1024);
if (builder.Configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
{
    // Backplane: lets hub messages reach clients connected to other instances of this service.
    signalR.AddStackExchangeRedis(redis, o => o.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("trailhub-groups"));
}

builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddSingleton<ILiveLocationNotifier, HubLiveLocationNotifier>();

var app = builder.Build();
app.UseTrailHubServiceDefaults();
app.MapHub<LocationHub>("/hubs/groups");

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateGroupsDatabaseAsync();
}

await app.RunAsync();
