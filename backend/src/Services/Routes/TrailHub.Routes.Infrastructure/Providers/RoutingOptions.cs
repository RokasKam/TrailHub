using Microsoft.Extensions.Configuration;
using TrailHub.Routes.Infrastructure.Providers.Mapbox;

namespace TrailHub.Routes.Infrastructure.Providers;

internal sealed record RoutingOptions(RoutingProvider Provider, string? OpenRouteServiceApiKey)
{
    public static RoutingOptions From(IConfiguration configuration, MapboxOptions mapbox)
    {
        var orsKey = configuration["Routing:OpenRouteService:ApiKey"];
        var provider = Enum.TryParse<RoutingProvider>(configuration["Routing:Provider"], ignoreCase: true, out var chosen) ? chosen
            : mapbox.Configured ? RoutingProvider.Mapbox
            : !string.IsNullOrWhiteSpace(orsKey) ? RoutingProvider.OpenRouteService
            : RoutingProvider.StraightLine;

        // A provider without its credentials can't work; fall back rather than fail every request.
        if ((provider == RoutingProvider.Mapbox && !mapbox.Configured) || (provider == RoutingProvider.OpenRouteService && string.IsNullOrWhiteSpace(orsKey)))
        {
            provider = RoutingProvider.StraightLine;
        }

        return new RoutingOptions(provider, orsKey);
    }
}
