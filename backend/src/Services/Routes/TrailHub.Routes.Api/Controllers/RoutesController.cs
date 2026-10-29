using System.Text;
using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Routes.Application.Routes;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Api.Controllers;

/// <summary>
/// Route planning: place waypoints and get a line along hiking paths with distance, climbing, walking time and an
/// elevation profile; save routes, split them into days, find alternatives, and plan multi-day hikes.
/// </summary>
/// <remarks>
/// Routing uses OpenRouteService's hiking profile when <c>Routing:OpenRouteService:ApiKey</c> is set; otherwise
/// routes are straight lines (<c>snapped: false</c>). Places to sleep, water and viewpoints come from OpenStreetMap.
/// Saved routes are private: another user's route id returns 404.
/// </remarks>
[Route("api/routes")]
public sealed class RoutesController : ApiController
{
    /// <summary>Routes waypoints without saving: what the editor shows while you place points.</summary>
    /// <remarks>Mark a waypoint <c>isOvernight</c> to end a day there; <c>days</c> then has one entry per day.</remarks>
    [HttpPost("preview")]
    [ProducesResponseType<RouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RouteDto>> Preview(WaypointsRequest body, [FromServices] IQueryHandler<PreviewRouteQuery, RouteDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new PreviewRouteQuery(body.Waypoints), ct));

    /// <summary>Up to three different routes between two points, best first.</summary>
    [HttpPost("alternatives")]
    [ProducesResponseType<IReadOnlyList<RouteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RouteDto>>> Alternatives(AlternativesRequest body, [FromServices] IQueryHandler<FindAlternativesQuery, IReadOnlyList<RouteDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new FindAlternativesQuery(body.From, body.To), ct));

    /// <summary>
    /// Plans a multi-day hike: routes start → finish, then picks a place to sleep each night (campsite, hut or hotel,
    /// as preferred) so the days are about equally long for the chosen difficulty and under the daily maximum.
    /// </summary>
    /// <remarks>Returns an unsaved route whose overnight waypoints are the stops. Save it with <c>POST /api/routes</c>.</remarks>
    [HttpPost("plan")]
    [ProducesResponseType<RouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RouteDto>> Plan(PlanItineraryQuery body, [FromServices] IQueryHandler<PlanItineraryQuery, RouteDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(body, ct));

    /// <summary>Water, campsites, huts, shelters, hotels and viewpoints within <c>corridorMeters</c> (default 500, max 2000) of a line.</summary>
    /// <remarks>Send the route's path as <c>[latitude, longitude]</c> pairs; it's thinned out server-side.</remarks>
    [HttpPost("pois")]
    [ProducesResponseType<IReadOnlyList<Poi>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Poi>>> Pois(PoisAlongQuery body, [FromServices] IQueryHandler<PoisAlongQuery, IReadOnlyList<Poi>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(body, ct));

    /// <summary>Searches places by name (towns, peaks, huts), ranked by distance from <c>lat</c>/<c>lon</c> when given.</summary>
    [HttpGet("places")]
    [ProducesResponseType<IReadOnlyList<Place>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Place>>> Places([FromQuery] string q, [FromQuery] double? lat, [FromQuery] double? lon, [FromServices] IQueryHandler<SearchPlacesQuery, IReadOnlyList<Place>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new SearchPlacesQuery(q, lat, lon), ct));

    /// <summary>The caller's saved routes, most recently changed first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RouteSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RouteSummary>>> List([FromServices] IQueryHandler<GetMyRoutesQuery, IReadOnlyList<RouteSummary>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetMyRoutesQuery(), ct));

    /// <summary>A saved route with its line, stats, elevation profile and days.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> Get(Guid id, [FromServices] IQueryHandler<GetRouteQuery, RouteDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetRouteQuery(id), ct));

    /// <summary>Saves a new route. The waypoints are routed again, so what's saved matches what was previewed.</summary>
    [HttpPost]
    [ProducesResponseType<RouteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RouteDto>> Create(SaveRouteRequest body, [FromServices] ICommandHandler<SaveRouteCommand, RouteDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new SaveRouteCommand(null, body.Name, body.Description, body.Waypoints), ct), route => CreatedAtAction(nameof(Get), new { id = route.Id }, route));

    /// <summary>Replaces a route's name, description and waypoints.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> Update(Guid id, SaveRouteRequest body, [FromServices] ICommandHandler<SaveRouteCommand, RouteDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new SaveRouteCommand(id, body.Name, body.Description, body.Waypoints), ct));

    /// <summary>Deletes a route.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] ICommandHandler<DeleteRouteCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new DeleteRouteCommand(id), ct));

    /// <summary>
    /// The route as GPX: the track (one segment per day), every waypoint with overnight stops marked, and with
    /// <c>pois=true</c> the water, huts, campsites and viewpoints along it.
    /// </summary>
    [HttpGet("{id:guid}/gpx")]
    [Produces("application/gpx+xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Gpx(Guid id, [FromQuery] bool pois, [FromServices] IQueryHandler<ExportRouteGpxQuery, (string FileName, string Gpx)> handler, CancellationToken ct)
    {
        var result = await handler.Handle(new ExportRouteGpxQuery(id, pois), ct);
        return result.IsSuccess
            ? File(Encoding.UTF8.GetBytes(result.Value.Gpx), "application/gpx+xml", result.Value.FileName)
            : ProblemFor(result.Error!);
    }
}

public sealed record WaypointsRequest(IReadOnlyList<WaypointInput> Waypoints);

public sealed record AlternativesRequest(WaypointInput From, WaypointInput To);

public sealed record SaveRouteRequest(string Name, string? Description, IReadOnlyList<WaypointInput> Waypoints);
