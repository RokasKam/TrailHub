using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trips.Application.Trips;
using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Api.Controllers;

/// <summary>Reusable packing lists. Add one to a trip with <c>POST /api/trips/{id}/checklist/from-list/{listId}</c>.</summary>
/// <remarks>Lists are private. The built-in starter lists (ids <c>starter:…</c>) are read-only.</remarks>
[Route("api/trips/gear-lists")]
public sealed class GearListsController : ApiController
{
    /// <summary>Your gear lists, then the starter lists.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GearListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GearListDto>>> List([FromServices] IQueryHandler<GetGearListsQuery, IReadOnlyList<GearListDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetGearListsQuery(), ct));

    /// <summary>Creates a gear list. Blank and duplicate items are dropped.</summary>
    [HttpPost]
    [ProducesResponseType<GearListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GearListDto>> Create(GearListRequest body, [FromServices] ICommandHandler<SaveGearListCommand, GearListDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new SaveGearListCommand(null, body.Name, body.Items), ct));

    /// <summary>Replaces a gear list's name and items.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<GearListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GearListDto>> Update(Guid id, GearListRequest body, [FromServices] ICommandHandler<SaveGearListCommand, GearListDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new SaveGearListCommand(id, body.Name, body.Items), ct));

    /// <summary>Deletes a gear list. Trips it was added to keep their items.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] ICommandHandler<DeleteGearListCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new DeleteGearListCommand(id), ct));
}

public sealed record GearListRequest(string Name, IReadOnlyList<GearItem> Items);
