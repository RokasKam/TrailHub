using Microsoft.Extensions.DependencyInjection;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Services;

namespace TrailHub.Trails.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTrailsApplication(this IServiceCollection services) =>
        services
            .AddScoped<ITrailService, TrailService>()
            .AddScoped<IPointOfInterestService, PointOfInterestService>()
            .AddScoped<ITrailPhotoService, TrailPhotoService>()
            .AddScoped<IReviewService, ReviewService>()
            .AddScoped<IConditionReportService, ConditionReportService>()
            .AddScoped<IFavoriteTrailService, FavoriteTrailService>()
            .AddScoped<IModerationService, ModerationService>()
            .AddScoped<IPublishedRouteService, PublishedRouteService>();
}
