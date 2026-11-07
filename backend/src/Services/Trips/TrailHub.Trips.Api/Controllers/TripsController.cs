using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trips.Application.Trips;
using TrailHub.Trips.Domain.Gear;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Api.Controllers;

/// <summary>Multi-day trip planning: dates, per-day trails and notes, and a packing checklist.</summary>
/// <remarks>
/// A trip is private until its owner shares it with one of their groups. Then every group member can see it
/// and use the checklist, while the plan itself (dates, days, trails) stays the owner's. A trip you can't see
/// returns 404. Every edit returns the updated trip, and people with the trip open get a <c>TripChanged</c>
/// message over SignalR at <c>/hubs/trips</c> (call <c>WatchTrip</c> first).
/// </remarks>
[Route("api/trips")]
public sealed class TripsController : ApiController
{
    /// <summary>The caller's own trips and trips shared with their groups, by start date.</summary>
    /// <param name="groupId">Only trips shared with this group (you must be a member).</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TripSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TripSummaryDto>>> List([FromQuery] Guid? groupId, [FromServices] IQueryHandler<GetMyTripsQuery, IReadOnlyList<TripSummaryDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetMyTripsQuery(groupId), ct));

    /// <summary>A trip with its day plans and checklist.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDto>> Get(Guid id, [FromServices] IQueryHandler<GetTripQuery, TripDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetTripQuery(id), ct));

    /// <summary>Creates a trip with one empty day plan per date (max 30 days), optionally shared with a group.</summary>
    [HttpPost]
    [ProducesResponseType<TripDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TripDto>> Create(CreateTripCommand body, [FromServices] ICommandHandler<CreateTripCommand, TripDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(body, ct), trip => CreatedAtAction(nameof(Get), new { id = trip.Id }, trip));

    /// <summary>Deletes a trip.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] ICommandHandler<DeleteTripCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new DeleteTripCommand(id), ct));

    /// <summary>Shares the trip with one of your groups, or makes it private again with <c>groupId: null</c>. Owner only.</summary>
    /// <remarks>Making it private, or moving it to another group, drops other people's claims and ticks.</remarks>
    [HttpPut("{id:guid}/group")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDto>> Share(Guid id, TripGroupRequest body, [FromServices] ICommandHandler<ShareTripCommand, TripDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new ShareTripCommand(id, body.GroupId), ct));

    /// <summary>Attaches a planned route (from the Routes service) to the trip, or detaches it with <c>routeId: null</c>. Owner only.</summary>
    /// <remarks>The route's day N is shown with the trip's day N. To let a group see it, share the route with the same group.</remarks>
    [HttpPut("{id:guid}/route")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> AttachRoute(Guid id, TripRouteRequest body, [FromServices] ICommandHandler<AttachRouteCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new AttachRouteCommand(id, body.RouteId), ct);

    /// <summary>Renames the trip or edits its description.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> UpdateDetails(Guid id, TripDetailsRequest body, [FromServices] ICommandHandler<UpdateTripDetailsCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new UpdateTripDetailsCommand(id, body.Name, body.Description), ct);

    /// <summary>Changes the dates. Existing day plans are kept by day number; days past the new end are dropped.</summary>
    [HttpPut("{id:guid}/dates")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> Reschedule(Guid id, TripDatesRequest body, [FromServices] ICommandHandler<RescheduleTripCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new RescheduleTripCommand(id, body.StartDate, body.EndDate), ct);

    /// <summary>Sets a day's notes and overnight stop.</summary>
    [HttpPut("{id:guid}/days/{day:int}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> UpdateDay(Guid id, int day, TripDayRequest body, [FromServices] ICommandHandler<UpdateTripDayCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new UpdateTripDayCommand(id, day, body.Notes, body.Overnight), ct);

    /// <summary>Adds a trail (from the Trails service) to a day.</summary>
    [HttpPut("{id:guid}/days/{day:int}/trails/{trailId:guid}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<TripDto>> AddTrail(Guid id, int day, Guid trailId, [FromServices] ICommandHandler<AddTrailToDayCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new AddTrailToDayCommand(id, day, trailId), ct);

    /// <summary>Removes a trail from a day.</summary>
    [HttpDelete("{id:guid}/days/{day:int}/trails/{trailId:guid}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> RemoveTrail(Guid id, int day, Guid trailId, [FromServices] ICommandHandler<RemoveTrailFromDayCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new RemoveTrailFromDayCommand(id, day, trailId), ct);

    /// <summary>Adds a checklist item: <c>Personal</c> (everyone brings their own, the default) or <c>Shared</c> group gear.</summary>
    [HttpPost("{id:guid}/checklist")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> AddChecklistItem(Guid id, ChecklistItemRequest body, [FromServices] ICommandHandler<AddChecklistItemCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new AddChecklistItemCommand(id, body.Text, body.Kind ?? ChecklistItemKind.Personal), ct);

    /// <summary>Adds several checklist items at once (e.g. picked suggestions). Items already on the list are skipped.</summary>
    [HttpPost("{id:guid}/checklist/bulk")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> AddChecklistItems(Guid id, ChecklistItemsRequest body, [FromServices] ICommandHandler<AddChecklistItemsCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new AddChecklistItemsCommand(id, body.Items), ct);

    /// <summary>Copies a gear list (yours, or a starter list like <c>starter:hut-trip</c>) into the checklist, skipping items it already has.</summary>
    /// <remarks>On a private trip, group gear is added as personal items.</remarks>
    [HttpPost("{id:guid}/checklist/from-list/{listId}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDto>> AddGearList(Guid id, string listId, [FromServices] ICommandHandler<AddGearListToTripCommand, TripDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new AddGearListToTripCommand(id, listId), ct));

    /// <summary>Saves the trip's checklist as one of your gear lists.</summary>
    [HttpPost("{id:guid}/checklist/save-as-list")]
    [ProducesResponseType<GearListDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GearListDto>> SaveAsGearList(Guid id, SaveAsGearListRequest body, [FromServices] ICommandHandler<SaveChecklistAsGearListCommand, GearListDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new SaveChecklistAsGearListCommand(id, body.Name), ct));

    /// <summary>
    /// Packing suggestions for this trip, each with the reason ("3 nights in huts", "Up to 2,900 m"). Items already on
    /// the checklist are left out. Send what the app knows about the planned route; anything missing is guessed
    /// from the trip's days.
    /// </summary>
    [HttpPost("{id:guid}/gear-suggestions")]
    [ProducesResponseType<IReadOnlyList<GearSuggestion>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GearSuggestion>>> GearSuggestions(Guid id, GearSuggestionsRequest body, [FromServices] IQueryHandler<GetGearSuggestionsQuery, IReadOnlyList<GearSuggestion>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetGearSuggestionsQuery(id, body.TentNights, body.HutNights, body.HotelNights, body.MaxElevationMeters, body.LongestDayMinutes), ct));

    /// <summary>"I'll bring it" on a shared item (<c>claim: true</c>), or taking it back. The owner can release anyone's claim.</summary>
    [HttpPut("{id:guid}/checklist/{itemId:guid}/claim")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<TripDto>> ClaimChecklistItem(Guid id, Guid itemId, ChecklistClaimRequest body, [FromServices] ICommandHandler<ClaimChecklistItemCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new ClaimChecklistItemCommand(id, itemId, body.Claim), ct);

    /// <summary>Ticks or unticks an item: your own copy of a personal item, or a shared item you're bringing (ticking an unclaimed one claims it).</summary>
    [HttpPut("{id:guid}/checklist/{itemId:guid}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> SetChecklistItemDone(Guid id, Guid itemId, ChecklistDoneRequest body, [FromServices] ICommandHandler<SetChecklistItemDoneCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new SetChecklistItemDoneCommand(id, itemId, body.IsDone), ct);

    /// <summary>Removes a checklist item. The owner can remove any item, others only the ones they added.</summary>
    [HttpDelete("{id:guid}/checklist/{itemId:guid}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> RemoveChecklistItem(Guid id, Guid itemId, [FromServices] ICommandHandler<RemoveChecklistItemCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new RemoveChecklistItemCommand(id, itemId), ct);

    /// <summary>Marks the trip completed. It becomes read-only.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> Complete(Guid id, [FromServices] ICommandHandler<CompleteTripCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new CompleteTripCommand(id), ct);

    /// <summary>Cancels the trip. It becomes read-only.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> Cancel(Guid id, [FromServices] ICommandHandler<CancelTripCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new CancelTripCommand(id), ct);

    private async Task<ActionResult<TripDto>> Send<TCommand>(ICommandHandler<TCommand, TripDto> handler, TCommand command, CancellationToken ct)
        where TCommand : TripChange =>
        FromResult(await handler.Handle(command, ct));
}

public sealed record TripDetailsRequest(string Name, string? Description);

public sealed record TripDatesRequest(DateOnly StartDate, DateOnly EndDate);

public sealed record TripDayRequest(string? Notes, string? Overnight);

public sealed record ChecklistItemRequest(string Text, ChecklistItemKind? Kind);

public sealed record ChecklistClaimRequest(bool Claim);

public sealed record TripGroupRequest(Guid? GroupId);

public sealed record TripRouteRequest(Guid? RouteId);

public sealed record ChecklistItemsRequest(IReadOnlyList<GearItem> Items);

public sealed record SaveAsGearListRequest(string Name);

public sealed record GearSuggestionsRequest(int? TentNights, int? HutNights, int? HotelNights, double? MaxElevationMeters, int? LongestDayMinutes);

public sealed record ChecklistDoneRequest(bool IsDone);
