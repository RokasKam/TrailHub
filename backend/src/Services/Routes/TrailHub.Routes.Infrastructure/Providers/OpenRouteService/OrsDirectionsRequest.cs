using System.Text.Json.Serialization;

namespace TrailHub.Routes.Infrastructure.Providers.OpenRouteService;

internal sealed record OrsDirectionsRequest(
    IReadOnlyList<double[]> Coordinates,
    bool Elevation,
    bool Instructions,
    [property: JsonPropertyName("extra_info")] IReadOnlyList<string> ExtraInfo,
    IReadOnlyList<int> Radiuses,
    [property: JsonPropertyName("alternative_routes")] OrsAlternativeRoutes? AlternativeRoutes);
