using TrailHub.BuildingBlocks.Web;
using TrailHub.Trails.Application;
using TrailHub.Trails.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Trails API");
builder.Services
    .AddTrailsApplication()
    .AddTrailsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTrailHubServiceDefaults();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateTrailsDatabaseAsync();
}

await app.RunAsync();
