using System.Text.Json.Serialization;

namespace TrailHub.Routes.Infrastructure.Providers.OpenRouteService;

internal sealed record OrsExtras([property: JsonPropertyName("traildifficulty")] OrsExtraValues? TrailDifficulty);
