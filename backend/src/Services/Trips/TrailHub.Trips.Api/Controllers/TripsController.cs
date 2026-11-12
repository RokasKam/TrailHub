using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Api.Controllers;

/// <summary>Multi-day trip planning: dates, per-day trails and notes, and a packing checklist.</summary>
/// <remarks>
/// A trip is private until its owner shares it with one of their groups. Then every group member can see it
/// and use the checklist, while the plan itself (dates, days, trails) stays the owner's. A trip you can't see
/// returns 404. Every edit returns the updated trip, and people with the trip open get a <c>TripChanged</c>
/// message over SignalR at <c>/hubs/trips</c> (call <c>WatchTrip</c> first).
/// </remarks>
[Route("api/trips")]
public sealed class TripsController(
    ITripService tripService,
    ITripChecklistService checklistService,
    IGearSuggestionService gearSuggestionService) : ApiController
{
    /// <summary>The caller's own trips and trips shared with their groups, by start date.</summary>
    /// <param name="groupId">Only trips shared with this group (you must be a member).</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TripSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TripSummaryResponse>>> List([FromQuery] Guid? groupId, CancellationToken ct) =>
        Ok(await tripService.GetTripsAsync(groupId, ct));

    /// <summary>A trip with its day plans and checklist.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await tripService.GetTripAsync(id, ct));

    /// <summary>Creates a trip with one empty day plan per date (max 30 days), optionally shared with a group.</summary>
    [HttpPost]
    [ProducesResponseType<TripResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TripResponse>> Create(CreateTripRequest body, CancellationToken ct)
    {
        var trip = await tripService.CreateAsync(body, ct);
        return CreatedAtAction(nameof(Get), new { id = trip.Id }, trip);
    }

    /// <summary>Deletes a trip.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tripService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Shares the trip with one of your groups, or makes it private again with <c>groupId: null</c>. Owner only.</summary>
    /// <remarks>Making it private, or moving it to another group, drops other people's claims and ticks.</remarks>
    [HttpPut("{id:guid}/group")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripResponse>> Share(Guid id, TripGroupRequest body, CancellationToken ct) =>
        Ok(await tripService.ShareAsync(id, body, ct));

    /// <summary>Sets the trip currency (ISO 4217, e.g. <c>CHF</c>) that expenses are converted to. Owner only.</summary>
    /// <remarks>Trips start in EUR. The currency can only change while no expenses or payments are logged (409 <c>Trip.CurrencyLocked</c>).</remarks>
    [HttpPut("{id:guid}/currency")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripResponse>> SetCurrency(Guid id, TripCurrencyRequest body, CancellationToken ct) =>
        Ok(await tripService.SetCurrencyAsync(id, body, ct));

    /// <summary>Attaches a planned route (from the Routes service) to the trip, or detaches it with <c>routeId: null</c>. Owner only.</summary>
    /// <remarks>The route's day N is shown with the trip's day N. To let a group see it, share the route with the same group.</remarks>
    [HttpPut("{id:guid}/route")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> AttachRoute(Guid id, TripRouteRequest body, CancellationToken ct) =>
        Ok(await tripService.AttachRouteAsync(id, body, ct));

    /// <summary>Renames the trip or edits its description.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> UpdateDetails(Guid id, TripDetailsRequest body, CancellationToken ct) =>
        Ok(await tripService.UpdateDetailsAsync(id, body, ct));

    /// <summary>Changes the dates. Existing day plans are kept by day number; days past the new end are dropped.</summary>
    [HttpPut("{id:guid}/dates")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> Reschedule(Guid id, TripDatesRequest body, CancellationToken ct) =>
        Ok(await tripService.RescheduleAsync(id, body, ct));

    /// <summary>Sets a day's notes and overnight stop.</summary>
    [HttpPut("{id:guid}/days/{day:int}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> UpdateDay(Guid id, int day, TripDayRequest body, CancellationToken ct) =>
        Ok(await tripService.UpdateDayAsync(id, day, body, ct));

    /// <summary>Adds a trail (from the Trails service) to a day.</summary>
    [HttpPut("{id:guid}/days/{day:int}/trails/{trailId:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripResponse>> AddTrail(Guid id, int day, Guid trailId, CancellationToken ct) =>
        Ok(await tripService.AddTrailAsync(id, day, trailId, ct));

    /// <summary>Removes a trail from a day.</summary>
    [HttpDelete("{id:guid}/days/{day:int}/trails/{trailId:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> RemoveTrail(Guid id, int day, Guid trailId, CancellationToken ct) =>
        Ok(await tripService.RemoveTrailAsync(id, day, trailId, ct));

    /// <summary>Adds a checklist item: <c>Personal</c> (everyone brings their own, the default) or <c>Shared</c> group gear.</summary>
    [HttpPost("{id:guid}/checklist")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> AddChecklistItem(Guid id, ChecklistItemRequest body, CancellationToken ct) =>
        Ok(await checklistService.AddItemAsync(id, body, ct));

    /// <summary>Adds several checklist items at once (e.g. picked suggestions). Items already on the list are skipped.</summary>
    [HttpPost("{id:guid}/checklist/bulk")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> AddChecklistItems(Guid id, ChecklistItemsRequest body, CancellationToken ct) =>
        Ok(await checklistService.AddItemsAsync(id, body, ct));

    /// <summary>Copies a gear list (yours, or a starter list like <c>starter:hut-trip</c>) into the checklist, skipping items it already has.</summary>
    /// <remarks>On a private trip, group gear is added as personal items.</remarks>
    [HttpPost("{id:guid}/checklist/from-list/{listId}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripResponse>> AddGearList(Guid id, string listId, CancellationToken ct) =>
        Ok(await checklistService.AddGearListAsync(id, listId, ct));

    /// <summary>Saves the trip's checklist as one of your gear lists.</summary>
    [HttpPost("{id:guid}/checklist/save-as-list")]
    [ProducesResponseType<GearListResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GearListResponse>> SaveAsGearList(Guid id, SaveAsGearListRequest body, CancellationToken ct) =>
        Ok(await checklistService.SaveAsGearListAsync(id, body, ct));

    /// <summary>
    /// Packing suggestions for this trip, each with the reason ("3 nights in huts", "Up to 2,900 m"). Items already on
    /// the checklist are left out. Send what the app knows about the planned route; anything missing is guessed
    /// from the trip's days.
    /// </summary>
    [HttpPost("{id:guid}/gear-suggestions")]
    [ProducesResponseType<IReadOnlyList<GearSuggestion>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GearSuggestion>>> GearSuggestions(Guid id, GearSuggestionsRequest body, CancellationToken ct) =>
        Ok(await gearSuggestionService.GetSuggestionsAsync(id, body, ct));

    /// <summary>"I'll bring it" on a shared item (<c>claim: true</c>), or taking it back. The owner can release anyone's claim.</summary>
    [HttpPut("{id:guid}/checklist/{itemId:guid}/claim")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripResponse>> ClaimChecklistItem(Guid id, Guid itemId, ChecklistClaimRequest body, CancellationToken ct) =>
        Ok(await checklistService.ClaimItemAsync(id, itemId, body, ct));

    /// <summary>Ticks or unticks an item: your own copy of a personal item, or a shared item you're bringing (ticking an unclaimed one claims it).</summary>
    [HttpPut("{id:guid}/checklist/{itemId:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> SetChecklistItemDone(Guid id, Guid itemId, ChecklistDoneRequest body, CancellationToken ct) =>
        Ok(await checklistService.SetItemDoneAsync(id, itemId, body, ct));

    /// <summary>Removes a checklist item. The owner can remove any item, others only the ones they added.</summary>
    [HttpDelete("{id:guid}/checklist/{itemId:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> RemoveChecklistItem(Guid id, Guid itemId, CancellationToken ct) =>
        Ok(await checklistService.RemoveItemAsync(id, itemId, ct));

    /// <summary>Marks the trip completed. It becomes read-only.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> Complete(Guid id, CancellationToken ct) =>
        Ok(await tripService.CompleteAsync(id, ct));

    /// <summary>Cancels the trip. It becomes read-only.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripResponse>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await tripService.CancelAsync(id, ct));
}
