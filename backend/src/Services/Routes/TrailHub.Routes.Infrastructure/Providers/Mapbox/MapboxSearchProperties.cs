using System.Text.Json.Serialization;

namespace TrailHub.Routes.Infrastructure.Providers.Mapbox;

internal sealed record MapboxSearchProperties(
    string? Name,
    [property: JsonPropertyName("place_formatted")] string? PlaceFormatted,
    [property: JsonPropertyName("feature_type")] string? FeatureType,
    MapboxSearchContext? Context);
