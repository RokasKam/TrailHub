using System.Text.Json.Serialization;

namespace TrailHub.Routes.Infrastructure.Providers.OpenRouteService;

internal sealed record OrsFeatureProperties([property: JsonPropertyName("way_points")] IReadOnlyList<int> WayPoints, OrsExtras? Extras);
