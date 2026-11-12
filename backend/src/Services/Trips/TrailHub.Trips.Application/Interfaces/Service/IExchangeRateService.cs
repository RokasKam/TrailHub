using TrailHub.Trips.Application.Responses;

namespace TrailHub.Trips.Application.Interfaces.Service;

/// <summary>Suggested exchange rates for expenses in another currency, and the currencies the app offers.</summary>
public interface IExchangeRateService
{
    /// <summary>
    /// Units of <paramref name="to"/> per one <paramref name="from"/> for <paramref name="date"/> (today when missing or in
    /// the future). The response's date is the rate's actual date, e.g. the previous business day.
    /// </summary>
    Task<ExchangeRateResponse> GetRateAsync(string? from, string? to, DateOnly? date, CancellationToken cancellationToken);

    /// <summary>The provider's currencies, or a built-in list of the ECB's when it can't be reached.</summary>
    Task<IReadOnlyList<CurrencyResponse>> GetCurrenciesAsync(CancellationToken cancellationToken);
}
