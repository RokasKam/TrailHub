using Microsoft.Extensions.DependencyInjection;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Services;

namespace TrailHub.Trips.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTripsApplication(this IServiceCollection services) =>
        services
            .AddScoped<TripEditor>()
            .AddScoped<ITripService, TripService>()
            .AddScoped<ITripChecklistService, TripChecklistService>()
            .AddScoped<IGearListService, GearListService>()
            .AddScoped<IGearSuggestionService, GearSuggestionService>()
            .AddScoped<IGroupMembershipService, GroupMembershipService>()
            .AddScoped<ITripExpenseService, TripExpenseService>()
            .AddScoped<IExchangeRateService, ExchangeRateService>();
}
