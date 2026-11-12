using System.Text.Json.Serialization;

namespace TrailHub.Trips.Infrastructure.Providers.Frankfurter;

/// <summary>One entry of <c>GET /v2/currencies</c>. <see cref="EndDate"/> is the last day with a rate.</summary>
internal sealed record FrankfurterCurrency(
    [property: JsonPropertyName("iso_code")] string IsoCode,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("end_date")] string? EndDate);
