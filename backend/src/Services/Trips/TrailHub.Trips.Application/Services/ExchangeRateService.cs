using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Models;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Services;

internal sealed class ExchangeRateService(IExchangeRateProvider provider, TimeProvider clock) : IExchangeRateService
{
    private const string Source = "ECB";

    public async Task<ExchangeRateResponse> GetRateAsync(string? from, string? to, DateOnly? date, CancellationToken cancellationToken)
    {
        if (CurrencyCode.Normalize(from) is not { } fromCode || CurrencyCode.Normalize(to) is not { } toCode)
        {
            throw TripErrors.InvalidCurrency.ToException();
        }

        // There are no rates for the future; a trip being planned gets today's.
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var day = date is { } requested && requested < today ? requested : today;
        if (fromCode == toCode)
        {
            return new ExchangeRateResponse(fromCode, toCode, day, 1m, Source);
        }

        var rate = (await provider.GetRateAsync(fromCode, toCode, day, cancellationToken)).GetValueOrThrow();
        return new ExchangeRateResponse(rate.From, rate.To, rate.Date, rate.Rate, rate.Source);
    }

    public async Task<IReadOnlyList<CurrencyResponse>> GetCurrenciesAsync(CancellationToken cancellationToken) =>
        ((await provider.GetCurrenciesAsync(cancellationToken)) ?? EcbCurrencies.All)
            .Select(c => new CurrencyResponse(c.Code, c.Name))
            .OrderBy(c => c.Code, StringComparer.Ordinal)
            .ToList();
}
