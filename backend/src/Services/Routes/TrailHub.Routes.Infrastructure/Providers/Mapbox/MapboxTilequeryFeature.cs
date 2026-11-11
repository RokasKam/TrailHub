using System.Text.Json;

namespace TrailHub.Routes.Infrastructure.Providers.Mapbox;

internal sealed record MapboxTilequeryFeature(JsonElement Id, MapboxTilequeryGeometry Geometry, MapboxTilequeryProperties Properties);
