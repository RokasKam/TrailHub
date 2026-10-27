using TrailHub.BuildingBlocks.Web;
using TrailHub.Trips.Application;
using TrailHub.Trips.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Trips API");
builder.Services
    .AddTripsApplication()
    .AddTripsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTrailHubServiceDefaults();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateTripsDatabaseAsync();
}

await app.RunAsync();
