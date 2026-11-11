using System.Text.Json.Serialization;

namespace TrailHub.Routes.Infrastructure.Providers.OpenRouteService;

internal sealed record OrsAlternativeRoutes(
    [property: JsonPropertyName("target_count")] int TargetCount,
    [property: JsonPropertyName("weight_factor")] double WeightFactor,
    [property: JsonPropertyName("share_factor")] double ShareFactor);
