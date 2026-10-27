using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trips.Application.Trips;

namespace TrailHub.Trips.Api.Controllers;

/// <summary>Multi-day trip planning: dates, per-day trails and notes, and a packing checklist.</summary>
/// <remarks>Trips are private; another user's trip id returns 404. Every edit returns the updated trip.</remarks>
[Route("api/trips")]
public sealed class TripsController : ApiController
{
    /// <summary>The caller's trips, by start date.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TripSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TripSummaryDto>>> List([FromServices] IQueryHandler<GetMyTripsQuery, IReadOnlyList<TripSummaryDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetMyTripsQuery(), ct));

    /// <summary>A trip with its day plans and checklist.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDto>> Get(Guid id, [FromServices] IQueryHandler<GetTripQuery, TripDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetTripQuery(id), ct));

    /// <summary>Creates a trip with one empty day plan per date (max 30 days).</summary>
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

    /// <summary>Adds a checklist item.</summary>
    [HttpPost("{id:guid}/checklist")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> AddChecklistItem(Guid id, ChecklistItemRequest body, [FromServices] ICommandHandler<AddChecklistItemCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new AddChecklistItemCommand(id, body.Text), ct);

    /// <summary>Ticks or unticks a checklist item.</summary>
    [HttpPut("{id:guid}/checklist/{itemId:guid}")]
    [ProducesResponseType<TripDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<TripDto>> SetChecklistItemDone(Guid id, Guid itemId, ChecklistDoneRequest body, [FromServices] ICommandHandler<SetChecklistItemDoneCommand, TripDto> handler, CancellationToken ct) =>
        Send(handler, new SetChecklistItemDoneCommand(id, itemId, body.IsDone), ct);

    /// <summary>Removes a checklist item.</summary>
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

public sealed record ChecklistItemRequest(string Text);

public sealed record ChecklistDoneRequest(bool IsDone);
