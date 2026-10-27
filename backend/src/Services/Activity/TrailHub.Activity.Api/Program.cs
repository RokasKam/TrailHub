using TrailHub.Activity.Application;
using TrailHub.Activity.Infrastructure;
using TrailHub.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Activity API");
builder.Services
    .AddActivityApplication()
    .AddActivityInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTrailHubServiceDefaults();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateActivityDatabaseAsync();
}

await app.RunAsync();
