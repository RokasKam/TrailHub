using System.Text;
using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Routes.Application.Interfaces.Service;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;

namespace TrailHub.Routes.Api.Controllers;

/// <summary>
/// Route planning: place waypoints and get a line along hiking paths with distance, climbing, walking time and an
/// elevation profile; save routes, split them into days, find alternatives, and plan multi-day hikes.
/// </summary>
/// <remarks>
/// Routing uses OpenRouteService's hiking profile when <c>Routing:OpenRouteService:ApiKey</c> is set; otherwise
/// routes are straight lines (<c>snapped: false</c>). Places to sleep, water and viewpoints come from OpenStreetMap.
/// A saved route is private until its author shares it with a group: members can then see it, follow it, export it and
/// copy it, but only the author can change, share or publish it. A route you can't see returns 404.
/// </remarks>
[Route("api/routes")]
public sealed class RoutesController(
    IRouteService routeService,
    IRoutePlanningService planningService,
    IRouteSharingService sharingService,
    IPlaceService placeService) : ApiController
{
    /// <summary>Routes waypoints without saving: what the editor shows while you place points.</summary>
    /// <remarks>Mark a waypoint <c>isOvernight</c> to end a day there; <c>days</c> then has one entry per day.</remarks>
    [HttpPost("preview")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RouteResponse>> Preview(WaypointsRequest body, CancellationToken ct) =>
        Ok(await planningService.PreviewAsync(body.Waypoints, ct));

    /// <summary>Up to three different routes between two points, best first.</summary>
    [HttpPost("alternatives")]
    [ProducesResponseType<IReadOnlyList<RouteResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RouteResponse>>> Alternatives(AlternativesRequest body, CancellationToken ct) =>
        Ok(await planningService.FindAlternativesAsync(body.From, body.To, ct));

    /// <summary>
    /// Plans a multi-day hike: routes start → finish, then picks a place to sleep each night (campsite, hut or hotel,
    /// as preferred) so the days are about equally long for the chosen difficulty and under the daily maximum.
    /// </summary>
    /// <remarks>Returns an unsaved route whose overnight waypoints are the stops. Save it with <c>POST /api/routes</c>.</remarks>
    [HttpPost("plan")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RouteResponse>> Plan(PlanItineraryRequest body, CancellationToken ct) =>
        Ok(await planningService.PlanItineraryAsync(body, ct));

    /// <summary>Water, campsites, huts, shelters, hotels and viewpoints within <c>corridorMeters</c> (default 500, max 2000) of a line.</summary>
    /// <remarks>Send the route's path as <c>[latitude, longitude]</c> pairs; it's thinned out server-side.</remarks>
    [HttpPost("pois")]
    [ProducesResponseType<IReadOnlyList<Poi>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Poi>>> Pois(PoisAlongRequest body, CancellationToken ct) =>
        Ok(await placeService.GetPoisAlongAsync(body, ct));

    /// <summary>Searches places by name (towns, peaks, huts), ranked by distance from <c>lat</c>/<c>lon</c> when given.</summary>
    [HttpGet("places")]
    [ProducesResponseType<IReadOnlyList<Place>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Place>>> Places([FromQuery] string q, [FromQuery] double? lat, [FromQuery] double? lon, CancellationToken ct) =>
        Ok(await placeService.SearchPlacesAsync(q, lat, lon, ct));

    /// <summary>Suggests a name for a route from its start and finish (huts, villages or towns).</summary>
    [HttpPost("suggest-name")]
    [ProducesResponseType<RouteNameResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RouteNameResponse>> SuggestName(WaypointsRequest body, CancellationToken ct) =>
        Ok(await placeService.SuggestNameAsync(body.Waypoints, ct));

    /// <summary>The caller's saved routes and routes shared with their groups (<c>isOwner: false</c>), most recently changed first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RouteSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RouteSummaryResponse>>> List(CancellationToken ct) =>
        Ok(await routeService.GetRoutesAsync(ct));

    /// <summary>A saved route with its line, stats, elevation profile and days.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await routeService.GetRouteAsync(id, ct));

    /// <summary>Saves a new route. The waypoints are routed again, so what's saved matches what was previewed.</summary>
    [HttpPost]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RouteResponse>> Create(SaveRouteRequest body, CancellationToken ct)
    {
        var route = await routeService.CreateAsync(body, ct);
        return CreatedAtAction(nameof(Get), new { id = route.Id }, route);
    }

    /// <summary>Replaces a route's name, description and waypoints.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteResponse>> Update(Guid id, SaveRouteRequest body, CancellationToken ct) =>
        Ok(await routeService.UpdateAsync(id, body, ct));

    /// <summary>Deletes a route.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await routeService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Shares the route with one of your groups. Members can see, follow, export and copy it.</summary>
    [HttpPut("{id:guid}/groups/{groupId:guid}")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteResponse>> Share(Guid id, Guid groupId, CancellationToken ct) =>
        Ok(await sharingService.ShareAsync(id, groupId, share: true, ct));

    /// <summary>Stops sharing the route with a group.</summary>
    [HttpDelete("{id:guid}/groups/{groupId:guid}")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RouteResponse>> Unshare(Guid id, Guid groupId, CancellationToken ct) =>
        Ok(await sharingService.ShareAsync(id, groupId, share: false, ct));

    /// <summary>Copies a route you can see (e.g. one shared with your group) into your own routes.</summary>
    [HttpPost("{id:guid}/copy")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RouteResponse>> Copy(Guid id, CancellationToken ct)
    {
        var route = await sharingService.CopyAsync(id, ct);
        return CreatedAtAction(nameof(Get), new { id = route.Id }, route);
    }

    /// <summary>
    /// Publishes the route to Discover as a public trail with the same id (or updates it when published again).
    /// Needs a route that follows paths and is at least 1 km long. Author only.
    /// </summary>
    /// <remarks>The trail appears in Discover a moment later: the Trails service creates it from an event.</remarks>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RouteResponse>> Publish(Guid id, PublishRouteRequest body, CancellationToken ct) =>
        Ok(await sharingService.PublishAsync(id, body, ct));

    /// <summary>Takes the route's trail out of Discover. Its reviews and photos are kept for when it's published again.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RouteResponse>> Unpublish(Guid id, CancellationToken ct) =>
        Ok(await sharingService.UnpublishAsync(id, ct));

    /// <summary>
    /// The route as GPX: the track (one segment per day), every waypoint with overnight stops marked, and with
    /// <c>pois=true</c> the water, huts, campsites and viewpoints along it.
    /// </summary>
    [HttpGet("{id:guid}/gpx")]
    [Produces("application/gpx+xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Gpx(Guid id, [FromQuery] bool pois, CancellationToken ct)
    {
        var file = await routeService.ExportGpxAsync(id, pois, ct);
        return File(Encoding.UTF8.GetBytes(file.Content), "application/gpx+xml", file.FileName);
    }
}
