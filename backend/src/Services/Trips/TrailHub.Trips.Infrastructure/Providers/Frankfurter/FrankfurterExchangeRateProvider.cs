using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Models;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Infrastructure.Providers.Frankfurter;

/// <summary>
/// European Central Bank reference rates through <see href="https://frankfurter.dev">Frankfurter</see> (free, no key).
/// Uses its v2 API restricted to the ECB provider (<c>providers=ecb</c>), so a weekend or holiday gets the previous
/// business day's rate. Rates and the currency list are cached in memory for 12 hours. Base URL: <c>ExchangeRates:BaseUrl</c>.
/// </summary>
internal sealed class FrankfurterExchangeRateProvider(HttpClient http, IMemoryCache cache, TimeProvider clock, ILogger<FrankfurterExchangeRateProvider> logger)
    : IExchangeRateProvider
{
    private const string Source = "ECB";
    private const string CurrenciesKey = "fx:currencies";
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(12);

    // Any failure (network, timeout or open circuit from the resilience handler, bad JSON) means "unavailable";
    // only the caller's own cancellation propagates.

    /// <summary>The ECB list keeps currencies it stopped quoting (e.g. HRK); only offer ones with a recent rate.</summary>
    private static readonly TimeSpan StillQuotedWithin = TimeSpan.FromDays(30);

    public async Task<Result<ExchangeRate>> GetRateAsync(string from, string to, DateOnly date, CancellationToken cancellationToken)
    {
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var key = $"fx:{from}:{to}:{day}";
        if (cache.TryGetValue(key, out ExchangeRate? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await http.GetAsync($"v2/rate/{Uri.EscapeDataString(from)}/{Uri.EscapeDataString(to)}?date={day}&providers=ecb", cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest)
            {
                return TripErrors.InvalidCurrency; // unknown currency, or no ECB rate for the pair on that date
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<FrankfurterRate>(cancellationToken);
            if (body is null || !DateOnly.TryParseExact(body.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var rateDate))
            {
                logger.LogWarning("Frankfurter returned an unreadable rate for {From}/{To} on {Date}", from, to, day);
                return TripErrors.RatesUnavailable;
            }

            var rate = Math.Round(body.Rate, Money.RateDecimals, MidpointRounding.AwayFromZero);
            if (rate <= 0)
            {
                return TripErrors.InvalidCurrency;
            }

            var result = new ExchangeRate(from, to, rateDate, rate, Source);
            cache.Set(key, result, CacheFor);
            return result;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Frankfurter rate lookup failed for {From}/{To} on {Date}", from, to, day);
            return TripErrors.RatesUnavailable;
        }
    }

    public async Task<IReadOnlyList<CurrencyInfo>?> GetCurrenciesAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CurrenciesKey, out IReadOnlyList<CurrencyInfo>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var body = await http.GetFromJsonAsync<List<FrankfurterCurrency>>("v2/currencies?providers=ecb", cancellationToken);
            var quotedSince = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime - StillQuotedWithin);
            var currencies = (body ?? [])
                .Where(c => c.Type is null or "currency"
                    && CurrencyCode.Normalize(c.IsoCode) == c.IsoCode
                    && (c.EndDate is null || (DateOnly.TryParseExact(c.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) && end >= quotedSince)))
                .Select(c => new CurrencyInfo(c.IsoCode, c.Name))
                .DistinctBy(c => c.Code)
                .ToList();
            if (currencies.Count == 0)
            {
                logger.LogWarning("Frankfurter returned no current currencies");
                return null;
            }

            cache.Set<IReadOnlyList<CurrencyInfo>>(CurrenciesKey, currencies, CacheFor);
            return currencies;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Frankfurter currency list failed");
            return null;
        }
    }
}
