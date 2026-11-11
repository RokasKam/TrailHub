namespace TrailHub.Routes.Infrastructure.Providers;

/// <summary>Which service draws routes along paths (<c>Routing:Provider</c>).</summary>
internal enum RoutingProvider
{
    /// <summary>Mapbox walking directions: the default whenever a Mapbox token is configured.</summary>
    Mapbox,

    /// <summary>OpenRouteService's hiking profile, which also reports SAC trail grades. Needs its own key.</summary>
    OpenRouteService,

    /// <summary>No routing service: straight lines between waypoints.</summary>
    StraightLine,
}
