namespace TrailHub.Routes.Domain.Routes;

public sealed record ItineraryOptions(int Days, double? MaxDailyDistanceMeters, Accommodation Accommodation, PlanDifficulty Difficulty)
{
    public const int MaxDays = 30;

    /// <summary>How long a day should be for this difficulty, in walking hours (breaks not included).</summary>
    public double TargetHours => Difficulty switch
    {
        PlanDifficulty.Easy => 4.5,
        PlanDifficulty.Moderate => 6,
        PlanDifficulty.Hard => 7.5,
        _ => 9,
    };

    /// <summary>Highest SAC grade that suits this difficulty (T2 / T3 / T4 / T6).</summary>
    public int MaxSacScale => Difficulty switch
    {
        PlanDifficulty.Easy => 2,
        PlanDifficulty.Moderate => 3,
        PlanDifficulty.Hard => 4,
        _ => 6,
    };
}
