namespace TrailHub.Trips.Domain.Expenses;

/// <summary>ISO 4217 currency codes: three letters, stored upper case.</summary>
public static class CurrencyCode
{
    public const string Default = "EUR";

    /// <summary>The code in upper case, or null when it isn't three letters.</summary>
    public static string? Normalize(string? code)
    {
        var trimmed = code?.Trim().ToUpperInvariant();
        return trimmed is { Length: 3 } && trimmed.All(c => c is >= 'A' and <= 'Z') ? trimmed : null;
    }
}
