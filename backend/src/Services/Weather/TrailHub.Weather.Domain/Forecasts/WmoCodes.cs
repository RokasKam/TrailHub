namespace TrailHub.Weather.Domain.Forecasts;

/// <summary>WMO weather interpretation codes as used by Open-Meteo and most national services.</summary>
public static class WmoCodes
{
    public static WeatherCondition ToCondition(int code) => code switch
    {
        0 => WeatherCondition.Clear,
        1 or 2 => WeatherCondition.PartlyCloudy,
        3 => WeatherCondition.Overcast,
        45 or 48 => WeatherCondition.Fog,
        51 or 53 or 55 => WeatherCondition.Drizzle,
        56 or 57 or 66 or 67 => WeatherCondition.FreezingRain,
        61 or 63 or 65 => WeatherCondition.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherCondition.Snow,
        80 or 81 or 82 => WeatherCondition.Showers,
        95 or 96 or 99 => WeatherCondition.Thunderstorm,
        _ => WeatherCondition.Unknown,
    };
}
