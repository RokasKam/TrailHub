using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Interfaces.Service;
using TrailHub.Routes.Application.Mappings;
using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Services;

internal sealed class RoutePlanningService(IRouter router, IPoiSource pois) : IRoutePlanningService
{
    public const int MaxAlternatives = 3;

    /// <summary>How far off the route a place to sleep may be.</summary>
    public const double OvernightCorridorMeters = 1500;

    private static readonly PoiKind[] SleepingPlaces = [PoiKind.Campsite, PoiKind.MountainHut, PoiKind.Shelter, PoiKind.Hotel];

    public async Task<RouteResponse> PreviewAsync(IReadOnlyList<WaypointRequest> waypoints, CancellationToken cancellationToken)
    {
        var (routedWaypoints, routed) = await router.RouteWaypointsAsync(waypoints, cancellationToken);
        return RouteMappings.Preview(routedWaypoints, routed);
    }

    public async Task<IReadOnlyList<RouteResponse>> FindAlternativesAsync(WaypointRequest from, WaypointRequest to, CancellationToken cancellationToken)
    {
        var waypoints = new[] { from.ToWaypoint(), to.ToWaypoint() };
        if (PlannedRoute.ValidateWaypoints(waypoints) is { } invalid)
        {
            throw invalid.ToException();
        }

        var found = await router.AlternativesAsync(
            new GeoPoint(from.Latitude, from.Longitude), new GeoPoint(to.Latitude, to.Longitude), MaxAlternatives, cancellationToken);
        return found.GetValueOrThrow().Select(r => RouteMappings.Preview(waypoints, r)).ToList();
    }

    public async Task<RouteResponse> PlanItineraryAsync(PlanItineraryRequest request, CancellationToken cancellationToken)
    {
        if (request.Days is < 1 or > ItineraryOptions.MaxDays || request.MaxDailyDistanceKm is <= 2 or > 100)
        {
            throw RouteErrors.InvalidPlan.ToException();
        }

        var planned = new List<WaypointRequest> { request.Start };
        planned.AddRange(request.Via ?? []);
        planned.Add(request.Finish);
        var (via, routed) = await router.RouteWaypointsAsync(planned, cancellationToken);

        var track = routed.Track;
        var options = new ItineraryOptions(request.Days, request.MaxDailyDistanceKm * 1000, request.Accommodation, request.Difficulty);
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
            throw RouteErrors.TooLong.ToException();
        }

        var final = await router.RouteAsync(waypoints.Select(w => new GeoPoint(w.Latitude, w.Longitude)).ToList(), cancellationToken);
        if (final.IsFailure)
        {
            // Routing to a hut can fail (e.g. it's only reachable by a path the router doesn't know); keep the first
            // route and end the days at the closest point on it instead.
            warnings.Add("Some stops couldn't be routed to exactly; days end at the closest point on the route.");
            var indices = waypoints.Select((w, i) => i == 0 ? 0 : i == waypoints.Count - 1 ? track.LastIndex : track.Nearest(new GeoPoint(w.Latitude, w.Longitude)).Index).ToList();
            return RouteMappings.Preview(waypoints, routed with { WaypointIndices = indices }, warnings);
        }

        var result = RouteMappings.Preview(waypoints, final.Value, warnings);
        return result with { Warnings = [.. result.Warnings, .. LongDays(result.Days, options)] };
    }

    /// <summary>Detours to the beds can make a day longer than planned; say which, so the user can adjust.</summary>
    private static IEnumerable<string> LongDays(IReadOnlyList<RouteDayResponse> days, ItineraryOptions options) =>
        days
            .Where(d => d.Stats.DurationMinutes / 60.0 > options.TargetHours * 1.5 || d.Stats.DistanceMeters > options.MaxDailyDistanceMeters)
            .Select(d => $"Day {d.Day} is long: {d.Stats.DistanceMeters / 1000:0.#} km, about {d.Stats.DurationMinutes / 60.0:0.#} h of walking.");
}
