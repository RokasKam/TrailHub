using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Routes.Domain.Routes;

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
