using TrailHub.BuildingBlocks.Web;
using TrailHub.Weather.Application;
using TrailHub.Weather.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailHubServiceDefaults("TrailHub Weather API");
builder.Services
    .AddWeatherApplication()
    .AddWeatherInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTrailHubServiceDefaults();

await app.RunAsync();
