namespace TrailHub.Routes.Domain.Routes;

/// <summary>
/// Walking time from the German/Swiss/Austrian alpine clubs' rule (DIN 33466): 4 km/h on the flat,
/// 300 m of ascent or 500 m of descent per hour; the larger of the horizontal and vertical times plus
/// half the smaller. It's what trail signposts in the Alps use, so estimates match what hikers see there.
/// </summary>
public static class HikingTime
{
    public const double FlatKmPerHour = 4;
    public const double AscentMetersPerHour = 300;
    public const double DescentMetersPerHour = 500;

    public static int EstimateMinutes(double distanceMeters, double ascentMeters, double descentMeters, double terrainFactor = 1)
    {
        var horizontal = distanceMeters / 1000 / FlatKmPerHour;
        var vertical = (ascentMeters / AscentMetersPerHour) + (descentMeters / DescentMetersPerHour);
        var hours = Math.Max(horizontal, vertical) + (Math.Min(horizontal, vertical) / 2);
        return (int)Math.Round(hours * terrainFactor * 60);
    }

    /// <summary>
    /// Rougher trails are slower than the rule's well-made paths. By SAC hiking scale:
    /// T1–T2 as is, T3 (exposed, some scrambling) +15%, T4 +30%, T5–T6 +50%. Unknown counts as T1.
    /// </summary>
    public static double TerrainFactor(int sacScale) => sacScale switch
    {
        <= 2 => 1.0,
        3 => 1.15,
        4 => 1.3,
        _ => 1.5,
    };
}
