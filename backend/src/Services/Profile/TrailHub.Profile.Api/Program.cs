using TrailHub.BuildingBlocks.Web;
using TrailHub.Profile.Application;
using TrailHub.Profile.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Profile API");
builder.Services
    .AddProfileApplication()
    .AddProfileInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTrailHubServiceDefaults();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateProfileDatabaseAsync();
}

await app.RunAsync();
