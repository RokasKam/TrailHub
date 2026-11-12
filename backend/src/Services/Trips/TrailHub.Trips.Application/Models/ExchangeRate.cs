namespace TrailHub.Trips.Application.Models;

/// <param name="Rate">Units of <paramref name="To"/> per one <paramref name="From"/>.</param>
/// <param name="Date">The day the rate is for (the provider's, e.g. the previous business day).</param>
/// <param name="Source">Who published it, e.g. "ECB".</param>
public sealed record ExchangeRate(string From, string To, DateOnly Date, decimal Rate, string Source);
