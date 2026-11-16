using System.Globalization;

namespace TrailHub.Notifications.Application.Services;

/// <summary>Formatting shared by the notification texts.</summary>
internal static class NotificationTexts
{
    /// <summary>"€30.00", "$12.50", "£8.00", or "CHF 30.00" for currencies without a common symbol.</summary>
    public static string Money(decimal amount, string currency)
    {
        var number = amount.ToString("N2", CultureInfo.InvariantCulture);
        return currency.ToUpperInvariant() switch
        {
            "EUR" => $"€{number}",
            "USD" => $"${number}",
            "GBP" => $"£{number}",
            var code => $"{code} {number}",
        };
    }

    /// <summary>"Sat 12 Jul" (UTC).</summary>
    public static string Day(DateTime utc) => utc.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    public static string Items(int count) => count == 1 ? "1 group item" : $"{count} group items";
}
