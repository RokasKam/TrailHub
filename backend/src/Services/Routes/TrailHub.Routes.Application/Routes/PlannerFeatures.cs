using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Routes;

/// <summary>Up to three different ways between two points, best first, so the user can pick one.</summary>
public sealed record FindAlternativesQuery(WaypointInput From, WaypointInput To) : IQuery<IReadOnlyList<RouteDto>>;

internal sealed class FindAlternativesHandler(IRouter router) : IQueryHandler<FindAlternativesQuery, IReadOnlyList<RouteDto>>
{
    public const int MaxAlternatives = 3;

    public async Task<Result<IReadOnlyList<RouteDto>>> Handle(FindAlternativesQuery query, CancellationToken cancellationToken)
    {
        var waypoints = new[] { query.From.ToWaypoint(), query.To.ToWaypoint() };
        if (PlannedRoute.ValidateWaypoints(waypoints) is { } invalid)
        {
            return invalid;
        }

        var found = await router.AlternativesAsync(
            new GeoPoint(query.From.Latitude, query.From.Longitude), new GeoPoint(query.To.Latitude, query.To.Longitude), MaxAlternatives, cancellationToken);
        return found.IsSuccess ? found.Value.Select(r => RouteViews.Preview(waypoints, r)).ToList() : found.Error!;
    }
}

/// <param name="Via">Optional places to pass through, in order.</param>
/// <param name="MaxDailyDistanceKm">Upper limit for a day's walk; null for no limit beyond what the difficulty suggests.</param>
public sealed record PlanItineraryQuery(
    WaypointInput Start,
    WaypointInput Finish,
    IReadOnlyList<WaypointInput>? Via,
    int Days,
    double? MaxDailyDistanceKm,
    Accommodation Accommodation,
    PlanDifficulty Difficulty) : IQuery<RouteDto>;

/// <summary>
/// The multi-day planner: routes start → finish, looks up places to sleep along the way, picks a stop for each
/// night (<see cref="ItineraryPlanner"/>), then routes again through those stops so each day really ends at the bed.
/// The answer is an unsaved route whose overnight waypoints are the stops; saving it is a normal route save.
/// </summary>
internal sealed class PlanItineraryHandler(IRouter router, IPoiSource pois) : IQueryHandler<PlanItineraryQuery, RouteDto>
{
    /// <summary>How far off the route a place to sleep may be.</summary>
    public const double OvernightCorridorMeters = 1500;

    private static readonly PoiKind[] SleepingPlaces = [PoiKind.Campsite, PoiKind.MountainHut, PoiKind.Shelter, PoiKind.Hotel];

    public async Task<Result<RouteDto>> Handle(PlanItineraryQuery query, CancellationToken cancellationToken)
    {
        if (query.Days is < 1 or > ItineraryOptions.MaxDays || query.MaxDailyDistanceKm is <= 2 or > 100)
        {
            return RouteErrors.InvalidPlan;
        }

        var planned = new List<WaypointInput> { query.Start };
        planned.AddRange(query.Via ?? []);
        planned.Add(query.Finish);
        var first = await router.RouteWaypointsAsync(planned, cancellationToken);
        if (first.IsFailure)
        {
            return first.Error!;
        }

        var (via, routed) = first.Value;
        var track = routed.Track;
        var options = new ItineraryOptions(query.Days, query.MaxDailyDistanceKm * 1000, query.Accommodation, query.Difficulty);
        var warnings = new List<string>();

        // Places to sleep, located on the track. Without map data the planner still works with plain stops.
        var found = await pois.AlongAsync(track.Points, OvernightCorridorMeters, SleepingPlaces, cancellationToken);
        if (found.IsFailure)
        {
            warnings.Add("Couldn't look up huts and campsites right now, so nights are at plain points on the route.");
        }

        var candidates = (found.IsSuccess ? found.Value : [])
            .Select(p =>
            {
                var location = new GeoPoint(p.Latitude, p.Longitude, p.Elevation);
                var (index, offTrack) = track.Nearest(location);
                return new OvernightCandidate(p.Name, p.Kind, location, index, offTrack);
            })
            .Where(c => c.OffTrackMeters <= OvernightCorridorMeters)
            .ToList();

        var itinerary = ItineraryPlanner.Plan(track, candidates, options);
        warnings.AddRange(itinerary.Warnings);

        // Waypoints for the final route: start, via points and overnight stops in route order, finish.
        var stops = itinerary.Stages
            .Where(s => s.Overnight is not null)
            .Select((s, i) => (Index: s.ToIndex, Waypoint: new RouteWaypoint(
                s.Overnight!.Location.Latitude,
                s.Overnight.Location.Longitude,
                s.Overnight.Name == ItineraryPlanner.FallbackName ? $"Night {i + 1}: no accommodation found" : s.Overnight.Name,
                IsOvernight: true,
                s.Overnight.Name == ItineraryPlanner.FallbackName ? null : s.Overnight.Kind)));
        var middle = via.Skip(1).Take(via.Count - 2)
            .Select((w, i) => (Index: routed.WaypointIndices[i + 1], Waypoint: w with { IsOvernight = false }));
        var waypoints = new List<RouteWaypoint> { via[0] };
        waypoints.AddRange(middle.Concat(stops).OrderBy(x => x.Index).Select(x => x.Waypoint));
        waypoints.Add(via[^1]);

        if (waypoints.Count > PlannedRoute.MaxWaypoints)
        {
            return RouteErrors.TooLong;
        }

        var final = await router.RouteAsync(waypoints.Select(w => new GeoPoint(w.Latitude, w.Longitude)).ToList(), cancellationToken);
        if (final.IsFailure)
        {
            // Routing to a hut can fail (e.g. it's only reachable by a path the router doesn't know); keep the first
            // route and end the days at the closest point on it instead.
            warnings.Add("Some stops couldn't be routed to exactly; days end at the closest point on the route.");
            var indices = waypoints.Select((w, i) => i == 0 ? 0 : i == waypoints.Count - 1 ? track.LastIndex : track.Nearest(new GeoPoint(w.Latitude, w.Longitude)).Index).ToList();
            return RouteViews.Preview(waypoints, routed with { WaypointIndices = indices }, warnings);
        }

        var result = RouteViews.Preview(waypoints, final.Value, warnings);
        return result with { Warnings = [.. result.Warnings, .. LongDays(result.Days, options)] };
    }

    /// <summary>Detours to the beds can make a day longer than planned; say which, so the user can adjust.</summary>
    private static IEnumerable<string> LongDays(IReadOnlyList<RouteDayDto> days, ItineraryOptions options) =>
        days
            .Where(d => d.Stats.DurationMinutes / 60.0 > options.TargetHours * 1.5 || d.Stats.DistanceMeters > options.MaxDailyDistanceMeters)
            .Select(d => $"Day {d.Day} is long: {d.Stats.DistanceMeters / 1000:0.#} km, about {d.Stats.DurationMinutes / 60.0:0.#} h of walking.");
}
