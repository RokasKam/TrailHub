using TrailHub.BuildingBlocks.Web;
using TrailHub.Notifications.Application;
using TrailHub.Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Notifications API");
builder.Services
    .AddNotificationsApplication()
    .AddNotificationsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTrailHubServiceDefaults();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateNotificationsDatabaseAsync();
}

await app.RunAsync();
