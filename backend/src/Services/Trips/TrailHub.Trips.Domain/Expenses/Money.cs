namespace TrailHub.Trips.Domain.Expenses;

/// <summary>Money rules shared by expenses and payments: two decimal places, rounded away from zero.</summary>
public static class Money
{
    /// <summary>Largest amount one expense can have, in its own currency.</summary>
    public const decimal MaxExpenseAmount = 1_000_000m;

    /// <summary>Largest amount that fits the <c>numeric(12,2)</c> columns (converted amounts and payments).</summary>
    public const decimal MaxStoredAmount = 9_999_999_999.99m;

    /// <summary>Largest exchange rate accepted (trip-currency units per one unit of the expense currency).</summary>
    public const decimal MaxRate = 1_000_000m;

    public const int RateDecimals = 6;

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static bool HasAtMostDecimals(decimal value, int places) => Math.Round(value, places) == value;

    /// <summary>A positive amount with at most two decimals, no larger than <paramref name="max"/>.</summary>
    public static bool IsValidAmount(decimal value, decimal max) => value > 0 && value <= max && HasAtMostDecimals(value, 2);

    public static decimal Convert(decimal amount, decimal rate) => Round(amount * rate);
}
