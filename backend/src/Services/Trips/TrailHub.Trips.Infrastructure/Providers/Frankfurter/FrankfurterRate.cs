using System.Text.Json.Serialization;

namespace TrailHub.Trips.Infrastructure.Providers.Frankfurter;

/// <summary><c>GET /v2/rate/{base}/{quote}</c>: <c>{"date":"2025-07-04","base":"CHF","quote":"EUR","rate":1.07}</c>.</summary>
internal sealed record FrankfurterRate(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("base")] string Base,
    [property: JsonPropertyName("quote")] string Quote,
    [property: JsonPropertyName("rate")] decimal Rate);
