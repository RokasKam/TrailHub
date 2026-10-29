using TrailHub.BuildingBlocks.Web;
using TrailHub.Routes.Application;
using TrailHub.Routes.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Routes API");
builder.Services
    .AddRoutesApplication()
    .AddRoutesInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTrailHubServiceDefaults();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateRoutesDatabaseAsync();
}

await app.RunAsync();
