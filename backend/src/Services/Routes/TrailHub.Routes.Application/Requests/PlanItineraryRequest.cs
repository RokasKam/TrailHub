using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Requests;

/// <param name="Via">Optional places to pass through, in order.</param>
/// <param name="MaxDailyDistanceKm">Upper limit for a day's walk; null for no limit beyond what the difficulty suggests.</param>
public sealed record PlanItineraryRequest(
    WaypointRequest Start,
    WaypointRequest Finish,
    IReadOnlyList<WaypointRequest>? Via,
    int Days,
    double? MaxDailyDistanceKm,
    Accommodation Accommodation,
    PlanDifficulty Difficulty);
