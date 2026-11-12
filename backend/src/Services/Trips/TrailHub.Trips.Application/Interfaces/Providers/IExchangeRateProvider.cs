using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Application.Models;

namespace TrailHub.Trips.Application.Interfaces.Providers;

/// <summary>Reference exchange rates from a third party. Implementations cache what they fetch.</summary>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Units of <paramref name="to"/> per one <paramref name="from"/> on <paramref name="date"/>, or the closest earlier
    /// day with a rate (weekends and holidays have none). Fails with <c>Trip.InvalidCurrency</c> for a currency the
    /// provider doesn't know and <c>Trip.RatesUnavailable</c> when it can't be reached.
    /// </summary>
    Task<Result<ExchangeRate>> GetRateAsync(string from, string to, DateOnly date, CancellationToken cancellationToken);

    /// <summary>The currencies the provider currently quotes, or null when it can't be reached.</summary>
    Task<IReadOnlyList<CurrencyInfo>?> GetCurrenciesAsync(CancellationToken cancellationToken);
}
