namespace TrailHub.Trips.Application.Responses;

/// <param name="Date">The rate's actual date, e.g. the previous business day.</param>
/// <param name="Rate">Units of <paramref name="To"/> per one <paramref name="From"/>.</param>
/// <param name="Source">Who published the rate, e.g. "ECB".</param>
public sealed record ExchangeRateResponse(string From, string To, DateOnly Date, decimal Rate, string Source);
