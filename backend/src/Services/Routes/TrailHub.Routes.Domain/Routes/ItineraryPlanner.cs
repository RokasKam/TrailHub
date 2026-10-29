using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Routes.Domain.Routes;

public enum PoiKind
{
    WaterSource,
    Campsite,
    MountainHut,

    /// <summary>Unstaffed shelter or bivouac hut.</summary>
    Shelter,

    /// <summary>Hotel, guesthouse, hostel or B&amp;B.</summary>
    Hotel,
    Viewpoint,
}

public enum Accommodation
{
    Tent,
    Hut,
    Hotel,
}

public enum PlanDifficulty
{
    Easy,
    Moderate,
    Hard,
    Expert,
}

/// <summary>A place to sleep near the route, located on the track.</summary>
/// <param name="TrackIndex">The track point closest to it.</param>
/// <param name="OffTrackMeters">How far it is from the track (walked there and back).</param>
public sealed record OvernightCandidate(string Name, PoiKind Kind, GeoPoint Location, int TrackIndex, double OffTrackMeters);

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

/// <summary>One day: track points <see cref="FromIndex"/> to <see cref="ToIndex"/>, ending at <see cref="Overnight"/> (null on the last day).</summary>
public sealed record PlannedStage(int FromIndex, int ToIndex, OvernightCandidate? Overnight);

public sealed record Itinerary(IReadOnlyList<PlannedStage> Stages, IReadOnlyList<string> Warnings);

/// <summary>
/// Splits a long route into days that end at a place to sleep. It picks the stops that make the days as even as
/// possible around the target walking time for the chosen difficulty, keeps each day under the maximum distance,
/// and prefers the chosen kind of accommodation. Where no accommodation is near enough, a day can end at a plain
/// point on the route ("no accommodation found"), so there is always an answer, flagged with a warning.
/// </summary>
public static class ItineraryPlanner
{
    /// <summary>Spacing of the fallback "no accommodation" stops along the track.</summary>
    public const double FallbackSpacingMeters = 2000;

    private const double Infeasible = 1e6;

    public static Itinerary Plan(Track track, IReadOnlyList<OvernightCandidate> candidates, ItineraryOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Days, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Days, ItineraryOptions.MaxDays);

        // Nodes in order along the track: start, candidate stops (real and fallback), finish.
        var stops = candidates
            .Where(c => c.TrackIndex > 0 && c.TrackIndex < track.LastIndex)
            .Concat(FallbackStops(track))
            .OrderBy(c => c.TrackIndex)
            .ThenBy(c => c.OffTrackMeters)
            .ToList();
        var nodes = new List<OvernightCandidate?> { null };
        nodes.AddRange(stops);
        nodes.Add(null);
        var index = nodes.Select((n, i) => i == 0 ? 0 : i == nodes.Count - 1 ? track.LastIndex : n!.TrackIndex).ToArray();
        var last = nodes.Count - 1;

        // Try the requested number of days first. Too short a route for that many days: use fewer. Too long to
        // fit within the limits: find the fewest days that do.
        var warnings = new List<string>();
        var plan = Solve(options.Days);
        if (double.IsPositiveInfinity(plan.Cost))
        {
            var fits = Enumerable.Range(1, options.Days - 1).Reverse().Select(Solve).First(p => !double.IsPositiveInfinity(p.Cost));
            warnings.Add($"The route is too short for {options.Days} days; planned {fits.Stages.Count} instead.");
            plan = fits;
        }
        else if (plan.Cost >= Infeasible)
        {
            var needed = Enumerable.Range(options.Days + 1, Math.Max(0, ItineraryOptions.MaxDays - options.Days))
                .Select(d => (Days: d, Plan: Solve(d)))
                .FirstOrDefault(x => x.Plan.Cost < Infeasible);
            if (needed.Plan.Stages is not null)
            {
                warnings.Add($"This route doesn't fit in {options.Days} day{(options.Days == 1 ? "" : "s")} within your limits; it needs about {needed.Days}.");
                plan = needed.Plan;
            }
            else
            {
                warnings.Add("Some days are longer than your limits; there's no closer place to stop.");
            }
        }
        else if (options.Days > 1 && track.Total.DurationMinutes / 60.0 < options.Days * options.TargetHours * 0.4)
        {
            warnings.Add($"The route is short for {options.Days} days; the days will be easy.");
        }

        if (plan.Stages.Any(s => s.Overnight is { Name: FallbackName }))
        {
            warnings.Add("Some nights have no accommodation nearby on the map. Check for wild camping rules or a detour.");
        }

        if (track.Total.MaxSacScale > options.MaxSacScale)
        {
            warnings.Add($"Parts of the route are graded T{track.Total.MaxSacScale}, harder than {options.Difficulty.ToString().ToLowerInvariant()}.");
        }

        return new Itinerary(plan.Stages, warnings);

        // Dynamic programming over (days used, stop reached): best[d][j] = cheapest way to reach node j in d days.
        (IReadOnlyList<PlannedStage> Stages, double Cost) Solve(int days)
        {
            var best = new double[days + 1, nodes.Count];
            var from = new int[days + 1, nodes.Count];
            for (var d = 0; d <= days; d++)
            {
                for (var j = 0; j < nodes.Count; j++)
                {
                    best[d, j] = double.PositiveInfinity;
                }
            }

            best[0, 0] = 0;
            for (var d = 1; d <= days; d++)
            {
                // The last day must end at the finish; earlier days must end at a stop.
                var targets = d == days ? Enumerable.Range(last, 1) : Enumerable.Range(1, last - 1);
                foreach (var j in targets)
                {
                    for (var i = d == 1 ? 0 : 1; i < j; i++)
                    {
                        if (double.IsPositiveInfinity(best[d - 1, i]) || index[i] > index[j] || (i > 0 && index[i] == index[j]))
                        {
                            continue;
                        }

                        var cost = best[d - 1, i] + DayCost(i, j);
                        if (cost < best[d, j])
                        {
                            best[d, j] = cost;
                            from[d, j] = i;
                        }
                    }
                }
            }

            var stages = new List<PlannedStage>();
            if (double.IsPositiveInfinity(best[days, last]))
            {
                return (stages, double.PositiveInfinity);
            }

            for (int d = days, j = last; d > 0; d--)
            {
                var i = from[d, j];
                stages.Add(new PlannedStage(index[i], index[j], nodes[j]));
                j = i;
            }

            stages.Reverse();
            return (stages, best[days, last]);
        }

        double DayCost(int i, int j)
        {
            // Walking to tonight's bed and back to the route tomorrow is part of the days.
            var detour = (nodes[i]?.OffTrackMeters ?? 0) + (nodes[j]?.OffTrackMeters ?? 0);
            var distance = track.DistanceAt(index[j]) - track.DistanceAt(index[i]) + detour;
            var hours = track.Minutes(index[i], index[j], detour) / 60.0;

            var cost = Math.Pow(hours - options.TargetHours, 2);
            if (options.MaxDailyDistanceMeters is { } max && distance > max)
            {
                cost += Infeasible;
            }

            if (hours > options.TargetHours * 1.5)
            {
                cost += Infeasible;
            }

            if (j != last && nodes[j] is { } stop)
            {
                cost += Mismatch(stop.Kind, stop.Name == FallbackName, options.Accommodation) + (stop.OffTrackMeters / 1000);
            }

            return cost;
        }
    }

    public const string FallbackName = "No accommodation found";

    /// <summary>
    /// How much worse a night at <paramref name="kind"/> is than the preferred accommodation, in the same units
    /// as an hour² of imbalance between days. Huts and shelters are fine for tent campers; a hotel guest
    /// would rather walk an extra hour than sleep in a bivouac.
    /// </summary>
    private static double Mismatch(PoiKind kind, bool fallback, Accommodation preferred) => (preferred, kind) switch
    {
        _ when fallback => preferred == Accommodation.Tent ? 4 : 12,
        (Accommodation.Tent, PoiKind.Campsite) => 0,
        (Accommodation.Tent, PoiKind.Shelter or PoiKind.MountainHut) => 1.5,
        (Accommodation.Tent, _) => 3,
        (Accommodation.Hut, PoiKind.MountainHut) => 0,
        (Accommodation.Hut, PoiKind.Shelter or PoiKind.Hotel) => 1.5,
        (Accommodation.Hut, _) => 3,
        (Accommodation.Hotel, PoiKind.Hotel) => 0,
        (Accommodation.Hotel, PoiKind.MountainHut) => 1.5,
        (Accommodation.Hotel, _) => 5,
        _ => 3,
    };

    private static IEnumerable<OvernightCandidate> FallbackStops(Track track)
    {
        var total = track.DistanceAt(track.LastIndex);
        for (var meters = FallbackSpacingMeters; meters < total - (FallbackSpacingMeters / 2); meters += FallbackSpacingMeters)
        {
            var i = track.IndexAtDistance(meters);
            yield return new OvernightCandidate(FallbackName, PoiKind.Campsite, track.Points[i], i, 0);
        }
    }
}
