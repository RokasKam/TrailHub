using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Routes.Domain.Routes;

/// <summary>A point the user placed. Overnight waypoints end a day, which is how a route becomes multi-day.</summary>
/// <param name="Kind">What the stop is, when it came from the map (e.g. the hut the planner chose).</param>
public sealed record RouteWaypoint(double Latitude, double Longitude, string? Name, bool IsOvernight, PoiKind? Kind = null);

/// <summary>The router's answer for a list of waypoints: a line along paths, and where each waypoint landed on it.</summary>
/// <param name="Snapped">False when the line is straight segments between waypoints (no hiking paths found, or no router configured).</param>
public sealed record RoutedTrack(Track Track, IReadOnlyList<int> WaypointIndices, bool Snapped);

/// <summary>
/// A route the user designed by placing waypoints, saved with the line the router produced so it can be shown,
/// measured and exported without routing it again.
/// </summary>
public sealed class PlannedRoute : AggregateRoot<Guid>
{
    public const int MaxNameLength = 200;
    public const int MaxTextLength = 4000;
    public const int MinWaypoints = 2;
    public const int MaxWaypoints = 50;
    public const double MinPublishDistanceMeters = 1000;

    private List<RouteWaypoint> _waypoints = [];
    private List<GeoPoint> _points = [];

    private PlannedRoute()
    {
    }

    public string OwnerId { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public IReadOnlyList<RouteWaypoint> Waypoints => _waypoints;

    /// <summary>The routed line, with elevation.</summary>
    public IReadOnlyList<GeoPoint> Points => _points;

    /// <summary>SAC trail grade per point (0 = unknown).</summary>
    public byte[] SacScale { get; private set; } = [];

    /// <summary>Index into <see cref="Points"/> where each waypoint sits.</summary>
    public int[] WaypointIndices { get; private set; } = [];

    public bool Snapped { get; private set; }

    public double DistanceMeters { get; private set; }

    public double AscentMeters { get; private set; }

    public double DescentMeters { get; private set; }

    public int DurationMinutes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Groups (in the Groups service) whose members can see, follow and copy this route.</summary>
    public List<Guid> SharedWithGroups { get; private set; } = [];

    /// <summary>When the route was last published to Discover, as a trail with the same id.</summary>
    public DateTime? PublishedAtUtc { get; private set; }

    public int DayCount => _waypoints.Count(w => w.IsOvernight) + 1;

    public static Result<PlannedRoute> Create(string ownerId, string name, string? description, IReadOnlyList<RouteWaypoint> waypoints, RoutedTrack routed, DateTime nowUtc)
    {
        var route = new PlannedRoute { Id = Guid.CreateVersion7(), OwnerId = ownerId, CreatedAtUtc = nowUtc };
        var result = route.Update(name, description, waypoints, routed, nowUtc);
        return result.IsSuccess ? route : result.Error!;
    }

    public Result Update(string name, string? description, IReadOnlyList<RouteWaypoint> waypoints, RoutedTrack routed, DateTime nowUtc)
    {
        if ((ValidateDetails(name, description) ?? ValidateWaypoints(waypoints)) is { } error)
        {
            return error;
        }

        if (routed.WaypointIndices.Count != waypoints.Count)
        {
            return RouteErrors.RoutingMismatch;
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        // The first and last waypoints can't be overnight stops: a day can't end where the route starts or finishes.
        _waypoints = waypoints
            .Select((w, i) => w with { Name = string.IsNullOrWhiteSpace(w.Name) ? null : w.Name.Trim(), IsOvernight = w.IsOvernight && i > 0 && i < waypoints.Count - 1 })
            .ToList();
        _points = [.. routed.Track.Points];
        SacScale = [.. routed.Track.SacScale];
        WaypointIndices = [.. routed.WaypointIndices];
        Snapped = routed.Snapped;

        var total = routed.Track.Total;
        DistanceMeters = total.DistanceMeters;
        AscentMeters = total.AscentMeters;
        DescentMeters = total.DescentMeters;
        DurationMinutes = total.DurationMinutes;
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Rename(string name, string? description, DateTime nowUtc)
    {
        if (ValidateDetails(name, description) is { } error)
        {
            return error;
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    public Track ToTrack() => new(_points, SacScale);

    public RoutedTrack ToRoutedTrack() => new(ToTrack(), WaypointIndices, Snapped);

    /// <summary>Lets members of a group see the route. Checking that the owner is in the group is the caller's job.</summary>
    public void ShareWithGroup(Guid groupId)
    {
        if (!SharedWithGroups.Contains(groupId))
        {
            SharedWithGroups.Add(groupId);
        }
    }

    public void UnshareGroup(Guid groupId) => SharedWithGroups.Remove(groupId);

    /// <summary>
    /// Checks the route can go public: it has to follow real paths (not straight lines) and be at least
    /// <see cref="MinPublishDistanceMeters"/> long, so Discover isn't filled with doodles.
    /// </summary>
    public Result MarkPublished(DateTime nowUtc)
    {
        if (!Snapped)
        {
            return RouteErrors.PublishNeedsPaths;
        }

        if (DistanceMeters < MinPublishDistanceMeters)
        {
            return RouteErrors.PublishTooShort;
        }

        PublishedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>A copy of this route owned by someone else, e.g. a group member who wants to change it.</summary>
    public PlannedRoute CopyFor(string ownerId, DateTime nowUtc) => Create(ownerId, Name, Description, Waypoints, ToRoutedTrack(), nowUtc).Value;

    /// <summary>The route's days: split at overnight waypoints. A route without any is a single day.</summary>
    public IReadOnlyList<(int FromIndex, int ToIndex, RouteWaypoint? Overnight)> Days() =>
        SplitIntoDays(_waypoints, WaypointIndices, _points.Count - 1);

    public static IReadOnlyList<(int FromIndex, int ToIndex, RouteWaypoint? Overnight)> SplitIntoDays(IReadOnlyList<RouteWaypoint> waypoints, IReadOnlyList<int> indices, int lastIndex)
    {
        var days = new List<(int, int, RouteWaypoint?)>();
        var start = 0;
        for (var i = 1; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i].IsOvernight)
            {
                days.Add((start, indices[i], waypoints[i]));
                start = indices[i];
            }
        }

        days.Add((start, lastIndex, null));
        return days;
    }

    public static Error? ValidateWaypoints(IReadOnlyList<RouteWaypoint> waypoints)
    {
        if (waypoints.Count is < MinWaypoints or > MaxWaypoints)
        {
            return RouteErrors.WaypointCount;
        }

        foreach (var w in waypoints)
        {
            if (GeoPoint.Create(w.Latitude, w.Longitude).IsFailure)
            {
                return RouteErrors.InvalidWaypoint;
            }

            if (w.Name?.Length > MaxNameLength)
            {
                return RouteErrors.TextTooLong;
            }
        }

        return null;
    }

    private static Error? ValidateDetails(string name, string? description) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength ? RouteErrors.InvalidName
        : description?.Length > MaxTextLength ? RouteErrors.TextTooLong
        : null;
}

public static class RouteErrors
{
    public static readonly Error NotFound = Error.NotFound("Route.NotFound", "The route does not exist.");
    public static readonly Error InvalidName = Error.Validation("Route.InvalidName", $"Name is required and must be at most {PlannedRoute.MaxNameLength} characters.");
    public static readonly Error TextTooLong = Error.Validation("Route.TextTooLong", "Text is too long.");
    public static readonly Error WaypointCount = Error.Validation("Route.WaypointCount", $"A route needs {PlannedRoute.MinWaypoints} to {PlannedRoute.MaxWaypoints} waypoints.");
    public static readonly Error InvalidWaypoint = Error.Validation("Route.InvalidWaypoint", "A waypoint has an invalid latitude or longitude.");
    public static readonly Error RoutingMismatch = Error.Validation("Route.RoutingMismatch", "The routed line doesn't match the waypoints.");
    public static readonly Error NoPath = Error.Validation("Route.NoPath", "No hiking path connects these points. Move a waypoint closer to a trail.");
    public static readonly Error TooLong = Error.Validation("Route.TooLong", "This route is too long to plan in one go. Add waypoints or plan it in parts.");
    public static readonly Error RouterUnavailable = Error.Unavailable("Route.RouterUnavailable", "The route planner is unavailable right now. Try again in a minute.");
    public static readonly Error MapDataUnavailable = Error.Unavailable("Route.MapDataUnavailable", "Map data (huts, water, campsites) is unavailable right now. Try again in a few minutes.");
    public static readonly Error InvalidPlan = Error.Validation("Route.InvalidPlan", $"Plan 1 to {ItineraryOptions.MaxDays} days with a sensible daily distance.");
    public static readonly Error NotOwner = Error.Forbidden("Route.NotOwner", "Only the route's author can do that.");
    public static readonly Error GroupNotFound = Error.NotFound("Route.GroupNotFound", "That group doesn't exist or you're not in it.");
    public static readonly Error PublishNeedsPaths = Error.Validation("Route.PublishNeedsPaths", "Only routes that follow paths can be published. This one is drawn as straight lines.");
    public static readonly Error PublishTooShort = Error.Validation("Route.PublishTooShort", "A route must be at least 1 km long to publish.");
    public static readonly Error InvalidPublishDetails = Error.Validation("Route.InvalidPublishDetails", "Give the trail a name and a region.");
    public static readonly Error PlaceNotFound = Error.NotFound("Route.PlaceNotFound", "No place matches that search.");
}
