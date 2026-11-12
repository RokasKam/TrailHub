namespace TrailHub.Trips.Application.Models;

/// <summary>The currencies the European Central Bank publishes reference rates for; used when the rate provider is down.</summary>
public static class EcbCurrencies
{
    public static readonly IReadOnlyList<CurrencyInfo> All =
    [
        new("AUD", "Australian Dollar"),
        new("BRL", "Brazilian Real"),
        new("CAD", "Canadian Dollar"),
        new("CHF", "Swiss Franc"),
        new("CNY", "Chinese Renminbi Yuan"),
        new("CZK", "Czech Koruna"),
        new("DKK", "Danish Krone"),
        new("EUR", "Euro"),
        new("GBP", "British Pound"),
        new("HKD", "Hong Kong Dollar"),
        new("HUF", "Hungarian Forint"),
        new("IDR", "Indonesian Rupiah"),
        new("ILS", "Israeli New Shekel"),
        new("INR", "Indian Rupee"),
        new("ISK", "Icelandic Króna"),
        new("JPY", "Japanese Yen"),
        new("KRW", "South Korean Won"),
        new("MXN", "Mexican Peso"),
        new("MYR", "Malaysian Ringgit"),
        new("NOK", "Norwegian Krone"),
        new("NZD", "New Zealand Dollar"),
        new("PHP", "Philippine Peso"),
        new("PLN", "Polish Złoty"),
        new("RON", "Romanian Leu"),
        new("SEK", "Swedish Krona"),
        new("SGD", "Singapore Dollar"),
        new("THB", "Thai Baht"),
        new("TRY", "Turkish Lira"),
        new("USD", "United States Dollar"),
        new("ZAR", "South African Rand"),
    ];
}
